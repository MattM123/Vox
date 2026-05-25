using System.Diagnostics;
using System.Runtime;
using System.Text;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Vox.Assets;
using Vox.Assets.Models;
using Vox.Enums;
using Vox.Genesis;
using Vox.Model;
using Vox.Rendering;
using Vox.UI.MenuLogic;

namespace Vox.UI
{
    public class ImGuiHelper : IImGuiHelper
    {
        private static int rotationAngle = 0;

        
        private Menu _currentMenu;

        private readonly int _inventoryVAO = GL.GenVertexArray();
        public static int _inventoryIconFBO;
        public static int _inventoryIconTexture;

        private readonly ImFontPtr PtFont18;
        private readonly ImFontPtr PtFont24;
        private readonly ImFontPtr PtFont72;

        private ITextureLoader? _textureLoader;
        private readonly IAssetLookup? _assetLookup;
        private readonly ISSBOManager? _ssboManager;
        private readonly IPlayer? _player;
        private readonly IRegionManager? _regionManager;
        private readonly ILightHelper? _lightHelper;
        private readonly IChunkCache? _chunkCache;
        private readonly ISettingsStore? _settings;
        private readonly ImGuiIOPtr _ioPtr;
        private readonly ImGuiController _UIController;

        private BlockType selectedBlock = BlockType.AIR;
        private static System.Numerics.Vector3 pickedColor = System.Numerics.Vector3.Zero;

        public ImGuiHelper(IAssetLookup assetLookup, ITextureLoader textureLoader, ISSBOManager ssboManager, IPlayer player, IRegionManager regionManager,
            ILightHelper lightHeler, IChunkCache chunkCache, ISettingsStore settings)
        {
            _textureLoader = textureLoader;
            _assetLookup = assetLookup;
            _ssboManager = ssboManager;
            _player = player;
            _regionManager = regionManager;
            _lightHelper = lightHeler;
            _chunkCache = chunkCache;
            _settings = settings;
            _ioPtr = ImGui.GetIO();

            string _assets = "..\\..\\..\\Assets\\";

            PtFont18 = _ioPtr.Fonts.AddFontFromFileTTF(Path.Combine(_assets, "Grid Hunter.ttf"), 18.0f);
            PtFont24 = _ioPtr.Fonts.AddFontFromFileTTF(Path.Combine(_assets, "Grid Hunter.ttf"), 24.0f);
            PtFont72 = _ioPtr.Fonts.AddFontFromFileTTF(Path.Combine(_assets, "Grid Hunter.ttf"), 72.0f);

        }
        public void CreateMainMenu()
        {
            float _guiScale = _settings!.GetSettings().GuiScale;
            float menuWidth = 20f * _guiScale;
            float menuHeight = 15f * _guiScale;

            ImGui.PushFont(PtFont18);

            float horizontalMenuScale = 3.5f;
            ImGui.Begin("World List", ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar);

            //Set menu style
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new System.Numerics.Vector4(0f, 0f, 0f, 0.3f));

            ImGui.Text("Choose a World");

            ImGui.BeginChild("World List Pane", new System.Numerics.Vector2(Window.screenWidth / horizontalMenuScale, menuHeight / 2.5f),
                ImGuiChildFlags.AlwaysUseWindowPadding | ImGuiChildFlags.AlwaysAutoResize);

            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(20f, 2f));
            ImGui.PushStyleVar(ImGuiStyleVar.SeparatorTextPadding, new System.Numerics.Vector2(20f, 2f));
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new System.Numerics.Vector2(10f, 10f));

            //Create UI entries for each world within world list
            try
            {
                IEnumerable<string> worldList = Directory.EnumerateDirectories(Window.GetAppFolder() + "worlds");
                int dirLen = worldList.Count();

                if (dirLen > 0)
                {
                    foreach (string folder in worldList)
                    {

                        //World label                  
                        string label = "";
                        string folderReverse = new([.. folder.Reverse()]);

                        for (int i = 0; i < folderReverse.Length - 1; i++)
                        {
                            if (folderReverse[i] != '\\')
                                label += folderReverse[i];
                            else
                                break;
                        }

                        ImGui.Text(new(label.Reverse().ToArray()));
                        ImGui.SameLine();

                        ImGui.SameLine(ImGui.GetWindowSize().X - 250, -1);
                        //Load button

                        ImGui.Button("Load World");
                        if (ImGui.IsItemClicked())
                        {
                            //Clear UI world load
                            SetCurrentMenu(Menu.None);

                            //Reset the menu chunk coordinates so the render in the world.  
                            foreach (Chunk c in Window.GetMenuChunks())
                                c.Reset();


                            Window.DisplayMainMenuScreen(false);
                            _regionManager?.ClearVisibleRegions();
                            _regionManager?.SetRegionDir(folder);
                        }
                        ImGui.SameLine();

                        //Delete button
                        ImGui.Button("Delete World");
                        if (ImGui.IsItemClicked())
                        {
                            try
                            {
                                Directory.Delete(folder, true);
                            }
                            catch (Exception e1)
                            {
                                Logger.Error(e1);
                            }
                        }
                    }
                }
                ImGui.Text("FPS: " + _ioPtr.Framerate);
            }
            catch (Exception e2)
            {
                Logger.Error(e2);

            }

            ImGui.PopStyleVar(3);
            ImGui.PopStyleColor();
            ImGui.EndChild();

            ImGui.PushItemWidth(Window.screenWidth / horizontalMenuScale);

            ImGui.SetKeyboardFocusHere();
            byte[] buffer = new byte[30];

            ImGui.InputText(" ", buffer, 30);

            buffer = [.. buffer.Reverse()];
            buffer = [.. buffer.SkipWhile(x => x == 0)];
            string worldName = Encoding.Default.GetString(buffer);

            ImGui.PopItemWidth();


            ImGui.Button("Create New World: " + Encoding.Default.GetString([.. buffer.Reverse()]));
            if (ImGui.IsItemClicked())
            {
                try
                {
                    Directory.CreateDirectory(Window.GetAppFolder() + "worlds\\" + Encoding.Default.GetString([.. buffer.Reverse()]));
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                    Logger.Debug(Window.GetAppFolder() + "worlds\\" + worldName);
                }
            }

            ImGui.PopFont();
            ImGui.End();
        }
        public void CreateDebugMenu()
        {
            ImGui.PushFont(PtFont18);

            /*=====================================
             Debug Display
             =====================================*/
            ImGui.Begin("Debug");

            ImGui.SetWindowPos(new System.Numerics.Vector2(0, 0));
            ImGui.SetWindowSize(new System.Numerics.Vector2(Window.screenWidth / 4.0f, Window.screenHeight / 2.0f));

            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f)));
            ImGui.Text("Player");
            ImGui.PopStyleColor();

            ImGui.Text($"Position: X:{_player!.GetPosition().X} Y:{_player.GetPosition().Y} Z:{_player.GetPosition().Z}");
            ImGui.Text($"Rotation: X:{_player.GetRotation().X}, Y:{_player.GetRotation().Y}");
            ImGui.Text("IsGrounded: " + _player.IsPlayerGrounded());

            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f)));
            ImGui.Text("World");
            ImGui.PopStyleColor();

            string playerRegioIdx = _regionManager!.GetRegionIndexFromChunkCoords((int)_player.GetPosition().X % _regionManager.GetChunkBounds(), (int)_player.GetPosition().Z % _regionManager.GetChunkBounds());
            ImGui.Text("Region: " + _regionManager.TryGetRegionFromFile(playerRegioIdx));
            ImGui.Text(_player.GetChunkWithPlayer().ToString());
            ImGui.Text("Chunks Surrounding Player: " + _chunkCache!.UpdateChunkCache().Count);

            ImGui.Text("");
            ImGui.Text("Chunks In Memory: " + _regionManager.PollChunkMemory());
            string str = "Regions In Memory:\n";
            foreach (KeyValuePair<string, Region> r in _chunkCache.GetRegions())
                str += $"[{r.Key}] {r.Value}\n";

            ImGui.Text(str);
            ImGui.Text("");

            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f)));
            ImGui.Text("Performance");
            ImGui.PopStyleColor();

            ImGui.Text("FPS: " + _ioPtr.Framerate);
            ImGui.Text("Memory: " + Utils.FormatSize(Process.GetCurrentProcess().WorkingSet64) + "/" + Utils.FormatSize(Process.GetCurrentProcess().PrivateMemorySize64));
            ImGui.Text("VRAM: " + Utils.FormatSize(Utils.GetTotalVRamUsage()) + "/" + Utils.FormatSize(Utils.GetTotalVramCommitted()));
            ImGui.Text("");

            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f)));
            ImGui.Text("Player Matrix");
            ImGui.PopStyleColor();

            ImGui.Text(_player.GetViewMatrix().ToString());
            ImGui.End();

            ImGui.PopFont();
        }
        public void CreateBlockColorPicker(Vector3 blockspace)
        {
            float _guiScale = _settings!.GetSettings().GuiScale;
            float menuWidth = 9.3f * _guiScale;
            float menuHeight = 7.2f * _guiScale;

            ImGui.SetNextWindowPos(new(Window.screenWidth / 2, Window.screenHeight / 2), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(_guiScale * 3, _guiScale * 3));

            if (ImGui.BeginPopup("ColorPicker"))
            {

                ImGui.PushFont(PtFont18);

                if (ImGui.ColorPicker3($"Red: {Math.Round(pickedColor.X * 15)} Green: {Math.Round(pickedColor.Y * 15)} Blue: {Math.Round(pickedColor.Z * 15)}", ref pickedColor))
                {
                    Chunk blockChunk = _regionManager!.GetAndLoadGlobalChunkFromCoords(blockspace);

                    //Syncronously executes in a separate thread so main thread isnt blocked during propagation and depropagation
                    CountdownEvent countdown = new(1);
                    ThreadPool.QueueUserWorkItem(new WaitCallback(delegate (object state)
                    {

                        _lightHelper!.SetBlockLight(blockspace, new ColorVector(0, 0, 0), blockChunk, true, false);
                        _regionManager.PropagateBlockLight(blockspace, BlockFace.UP, true, true);
                        _regionManager.PropagateBlockLight(blockspace, BlockFace.DOWN, true, true);
                        _regionManager.PropagateBlockLight(blockspace, BlockFace.EAST, true, true);
                        _regionManager.PropagateBlockLight(blockspace, BlockFace.WEST, true, true);
                        _regionManager.PropagateBlockLight(blockspace, BlockFace.NORTH, true, true);
                        _regionManager.PropagateBlockLight(blockspace, BlockFace.SOUTH, true, true);
                        _lightHelper.GetLightTrackingList().Remove(blockspace);

                        _regionManager.AddBlockToChunk(
                            blockspace,
                            BlockType.LAMP_BLOCK,
                            new ColorVector(
                                (int)Math.Round(pickedColor.X * 15),
                                (int)Math.Round(pickedColor.Y * 15),
                                (int)Math.Round(pickedColor.Z * 15)
                            ), false
                        );
                        countdown.Signal();
                    }));
                    countdown.Wait();
                }
                ImGui.EndPopup();
                ImGui.PopFont();
            }
        }
        public void CreatePlayerInventory()
        {
            ImGui.PushFont(PtFont18);

            int topFillerPadding = 5;
            int slotPadding = 5;
            float _guiScale = _settings!.GetSettings().GuiScale;
            float menuWidth = 9.3f * _guiScale;
            float menuHeight = 7.2f * _guiScale;
            float slotSize = (menuWidth + slotPadding) / 9;
            float hotbarSpacing = _guiScale / 3.5f;
            System.Numerics.Vector2 inventorySlotSize = new(slotSize - (slotPadding * 3f), slotSize - (slotPadding * 3.5f));

            rotationAngle += 1;
            if (rotationAngle > 360)
                rotationAngle = 0;

            //Main window sizing and position
            System.Numerics.Vector2 center = ImGui.GetMainViewport().GetCenter();
            ImGui.SetNextWindowPos(new(center.X - (menuWidth / 2), center.Y - (menuHeight / 2)), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new(menuWidth, menuHeight));

            // Inventory Top UI styling
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new System.Numerics.Vector2(topFillerPadding, topFillerPadding));
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new System.Numerics.Vector4(29 / 255, 26 / 255, 29 / 255, 1f));

            ImGui.Begin("PlayerInventory", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove);

            System.Numerics.Vector2 inventoryFillerSize = new((menuWidth / 3) - (topFillerPadding * 3.5f), _guiScale * 2.5f);
            ImGui.BeginChild("InventoryTop", new System.Numerics.Vector2(menuWidth, 2.7f * _guiScale));
            ImGui.ImageButton("TopFiller", 0, inventoryFillerSize);
            ImGui.SameLine();
            ImGui.ImageButton("TopFiller", 0, inventoryFillerSize);
            ImGui.SameLine();

            if (selectedBlock != BlockType.AIR)
            {
                // Render block model for current slot
                InventoryStore inventory = _player!.GetInventory();
                BlockModel model = ModelLoader.GetModel(selectedBlock);
                string modelElements = model.GetElements().ElementAt(0).ToString();

                int start = modelElements.IndexOf("from=(") + 6;
                int end = modelElements.IndexOf(")", start);

                string coords = modelElements[start..end];
                string[] parts = coords.Split(',');


                int x = int.Parse(parts[0].Trim());
                int y = int.Parse(parts[1].Trim());
                int z = int.Parse(parts[2].Trim());

                inventory.AddOrUpdateFaceInMemory(
                new BlockFaceInstance
                {
                    facePosition = new OpenTK.Mathematics.Vector3(x, y, z),
                    index = 0,
                    lighting = 0,
                    textureLayer = (int)model.GetTexture(BlockFace.NORTH),
                    faceDirection = (int)BlockFace.NORTH
                });
                inventory.AddOrUpdateFaceInMemory(
                    new BlockFaceInstance
                    {
                        facePosition = new OpenTK.Mathematics.Vector3(x, y, z),
                        index = 1,
                        lighting = 56149,
                        textureLayer = (int)model.GetTexture(BlockFace.SOUTH),
                        faceDirection = (int)BlockFace.SOUTH
                    });
                inventory.AddOrUpdateFaceInMemory(
                    new BlockFaceInstance
                    {
                        facePosition = new OpenTK.Mathematics.Vector3(x, y, z),
                        index = 2,
                        lighting = 56149,
                        textureLayer = (int)model.GetTexture(BlockFace.EAST),
                        faceDirection = (int)BlockFace.EAST
                    });
                inventory.AddOrUpdateFaceInMemory(
                    new BlockFaceInstance
                    {
                        facePosition = new OpenTK.Mathematics.Vector3(x, y, z),
                        index = 3,
                        lighting = 56149,
                        textureLayer = (int)model.GetTexture(BlockFace.WEST),
                        faceDirection = (int)BlockFace.WEST
                    });
                inventory.AddOrUpdateFaceInMemory(
                    new BlockFaceInstance
                    {
                        facePosition = new OpenTK.Mathematics.Vector3(x, y, z),
                        index = 4,
                        lighting = 56149,
                        textureLayer = (int)model.GetTexture(BlockFace.UP),
                        faceDirection = (int)BlockFace.UP
                    });
                inventory.AddOrUpdateFaceInMemory(
                    new BlockFaceInstance
                    {
                        facePosition = new OpenTK.Mathematics.Vector3(x, y, z),
                        index = 5,
                        lighting = 56149,
                        textureLayer = (int)model.GetTexture(BlockFace.DOWN),
                        faceDirection = (int)BlockFace.DOWN
                    });

                RenderInventoryAnimation(256, 256);
                ImGui.ImageButton("Info Panel", _inventoryIconTexture, inventoryFillerSize);
            }
            else
            {
                ImGui.ImageButton("Info Panel", 0, inventoryFillerSize);
            }

            ImGui.PopStyleColor(1);
            ImGui.PopStyleVar(1);
            ImGui.EndChild();

            //Inventory UI Stying
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 8f);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0.5f);
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new System.Numerics.Vector2(slotPadding, slotPadding));
            ImGui.PushStyleColor(ImGuiCol.Border, new System.Numerics.Vector4(0.05f, 0.05f, 0.05f, 1f));
            ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.15f, 0.15f, 0.15f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0f, 0f, 0f, 0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new System.Numerics.Vector4(0.35f, 0.35f, 0.35f, 1f));


            //Populate inventory slots
            Dictionary<int, KeyValuePair<BlockType, int>> inventorySlots = _player!.GetInventory().GetSlots();
            for (int i = 0; i < inventorySlots.Count; i++)
            {
                ImGui.PushID(i);
                BlockType currentSlotType = inventorySlots[i].Key;



                if (currentSlotType != BlockType.AIR)
                {

                    // Calculate button position and size before drawing
                    var cursorPos = ImGui.GetCursorScreenPos();
                    var buttonRectMin = cursorPos;
                    var buttonRectMax = new System.Numerics.Vector2(cursorPos.X + inventorySlotSize.X, cursorPos.Y + inventorySlotSize.Y);
                    var mousePos = ImGui.GetMousePos();

                    // Determine hover state by manual check *before* drawing
                    bool isHovered = mousePos.X >= buttonRectMin.X && mousePos.X <= buttonRectMax.X &&
                                     mousePos.Y >= buttonRectMin.Y && mousePos.Y <= buttonRectMax.Y;

                    // Select texture image for slot
                    IntPtr textureToUse = _textureLoader!.LoadSingleTexture(_assetLookup!.GetFileFromBlockType(currentSlotType));

                    if (isHovered)
                    {
                        selectedBlock = inventorySlots[i].Key;
                        ImGui.SetTooltip($"{selectedBlock}\nQuantity: {inventorySlots[i].Value}");

                        ImGui.ImageButton("##" + i, _inventoryIconTexture, inventorySlotSize);

                    }
                    else
                    {
                        ImGui.ImageButton("##" + i, textureToUse, inventorySlotSize);
                    }
                    // Draw rectangle around the button
                    System.Numerics.Vector2 min = ImGui.GetItemRectMin();
                    System.Numerics.Vector2 max = ImGui.GetItemRectMax();

                    var drawList = ImGui.GetWindowDrawList();

                    uint color = isHovered ? ImGui.GetColorU32(new System.Numerics.Vector4(0, 0, 1, 1)) // blue if hovered
                                         : ImGui.GetColorU32(new System.Numerics.Vector4(1, 1, 1, 1)); // white if not hovered

                    drawList.AddRect(min, max, color, rounding: 3.0f, ImDrawFlags.None, thickness: 2.0f);

                    // Show item quantity in top left corner
                    drawList.AddText(ImGui.GetFont(), 15, min + new System.Numerics.Vector2(5, 5), color,
                        _player.GetInventory().GetSlots()[i].Value.ToString());

                }
                else
                {
                    //Create ImageButtom
                    ImGui.ImageButton("##" + i, 0, inventorySlotSize);

                }
                ImGui.PopID();

                //Splits first 36 slots into rows of 9
                if ((i + 1) % 9 != 0)
                    ImGui.SameLine();

                //Spacing between hotbar and inventory rows                
                if (i == 26)
                    ImGui.Dummy(new(0, hotbarSpacing));


            }

            ImGui.PopStyleVar(3);
            ImGui.PopStyleColor(4);
            ImGui.PopFont();
            ImGui.End();
        }
        public void CreatePauseMenu()
        {
            //Set menu sizing
            float _guiScale = _settings!.GetSettings().GuiScale;
            float menuWidth = 9.3f * _guiScale;
            float menuHeight = 7.2f * _guiScale;

            //Main window sizing and position
            System.Numerics.Vector2 center = ImGui.GetMainViewport().GetCenter();
            ImGui.SetNextWindowPos(new(center.X - (menuWidth / 2), center.Y - (menuHeight / 2)), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new(menuWidth, menuHeight));

            // Main window UI styling
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new System.Numerics.Vector4(29 / 255, 26 / 255, 29 / 255, 0.5f));

            // Menu creation
            ImGui.Begin("Paused", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove);

            // Menu title
            ImGui.PushFont(PtFont72);
            Utils.CreateCenteredText("Paused");
            ImGui.PopFont();

            ImGui.PushFont(PtFont24);

            // Menu filler
            ImGui.BeginChild("Filler", new(menuWidth, menuHeight / 10));
            ImGui.EndChild();

            if (Utils.ButtonCentered("Settings", _guiScale / 2, _guiScale / 6))
            {
                _settings.FillBuffersFromSettings();
                SetCurrentMenu(Menu.Settings);
            }
            if (Utils.ButtonCentered("Back To Game", _guiScale / 2, _guiScale / 6))
            {
                SetCurrentMenu(Menu.None);
            }
            if (Utils.ButtonCentered("Back to Main Menu", _guiScale / 2, _guiScale / 6))
            {
                SetCurrentMenu(Menu.Main);
                Window.DisplayMainMenuScreen(true);
            }

            ImGui.End();

            // Pop main window UI styling
            ImGui.PopStyleColor(1);
            ImGui.PopFont();
        }
        public void CreateSettingsMenu()
        {

            //Set menu sizing
            float _guiScale = _settings!.GetSettings().GuiScale;
            float menuWidth = 9.3f * _guiScale;
            float menuHeight = 7.2f * _guiScale;

            //Main window sizing and position
            System.Numerics.Vector2 center = ImGui.GetMainViewport().GetCenter();
            ImGui.SetNextWindowPos(new(center.X - (menuWidth / 2), center.Y - (menuHeight / 2)), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new(menuWidth, menuHeight));

            // Main window UI styling
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new System.Numerics.Vector4(29 / 255, 26 / 255, 29 / 255, 0.5f));

            // Menu creation
            ImGui.Begin("Settings", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove);

            // Menu title
            ImGui.PushFont(PtFont72);
            Utils.CreateCenteredText("Settings");
            ImGui.PopFont();

            ImGui.PushFont(PtFont24);

            // Menu filler
            ImGui.BeginChild("Filler", new(menuWidth, menuHeight / 10));
            ImGui.EndChild();

            float val = _settings.GetSettings().GuiScaleBuffer;
            if (ImGui.SliderFloat("GUI Scale", ref val, 50f, 150f, "%.1f", ImGuiSliderFlags.AlwaysClamp | ImGuiSliderFlags.Logarithmic))
            {
                _settings.GetSettings().GuiScaleBuffer = val;
            }
            if (Utils.ButtonCentered("Apply Settings", _guiScale / 2, _guiScale / 6))
            {
                _settings.SetGuiScale(_settings.GetSettings().GuiScaleBuffer);
                _settings.SaveSettingsToFile();
            }
            if (Utils.ButtonCentered("Back", _guiScale / 2, _guiScale / 6))
            {
                SetCurrentMenu(Menu.Pause);
            }

            ImGui.End();

            // Pop main window UI styling
            ImGui.PopStyleColor(1);
            ImGui.PopFont();
        }
        private void RenderInventoryAnimation(int sizeX, int sizeY)
        {
            //Save current state
            int prevFBO = GL.GetInteger(GetPName.FramebufferBinding);
            int prevProgram = GL.GetInteger(GetPName.CurrentProgram);
            int prevVAO = GL.GetInteger(GetPName.VertexArrayBinding);
            int[] prevViewport = new int[4];
            GL.GetInteger(GetPName.Viewport, prevViewport);

            //FBO setup FIRST, before viewport
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _inventoryIconFBO);

            FramebufferErrorCode status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
            {
                Console.WriteLine($"Framebuffer error: {status}");
                return;
            }

            GL.BindVertexArray(_inventoryVAO);
            GL.Viewport(0, 0, sizeX, sizeY);

            // Bind SSBO for vertex data
            var inventorySsbo = _ssboManager!.GetSSBO(SSBO.Inventory);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, inventorySsbo.Handle);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, inventorySsbo.BindingIndex, inventorySsbo.Handle);

            GL.ClearColor(0, 0, 0, 0);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            //Drawing
            GL.DrawArraysInstanced(
                PrimitiveType.TriangleStrip,  // Drawing a triangle strip
                0,                            // Start from the first vertex in the base geometry
                4,                            // 4 vertices per face (for triangle strip)
                6                             // Instance count (number of faces to draw)
            );



            //Restore state
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, prevFBO);
            GL.UseProgram(prevProgram);
            GL.BindVertexArray(prevVAO);
            GL.Viewport(prevViewport[0], prevViewport[1], prevViewport[2], prevViewport[3]);

        }

        public void SetCurrentMenu(Menu menu)
        {
            _currentMenu = menu;
        }
        public Menu GetCurrentMenu()
        {
            return _currentMenu;
        }
        public void RenderCurrentMenu()
        {
            if (_currentMenu == Menu.Inventory)
                CreatePlayerInventory();
            else if (_currentMenu == Menu.Settings)
                CreateSettingsMenu();
            else if (_currentMenu == Menu.ColorPicker && _player != null) { 
                CreateBlockColorPicker(_player.UpdateViewTarget(out _, out _, out _));
                ImGui.OpenPopup("ColorPicker");
            }
            else if (_currentMenu == Menu.Pause)
                CreatePauseMenu();
            else if (_currentMenu == Menu.Main)
                CreateMainMenu();
            else if (_currentMenu == Menu.Debug)
                CreateDebugMenu();
        
        }
    }
}