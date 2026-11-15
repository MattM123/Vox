using System.Diagnostics;
using System.Text;
using ImGuiNET;
using OpenTK.Mathematics;
using Vox.Assets;
using Vox.Enums;
using Vox.Genesis;
using Vox.Model;
using Vox.Rendering;
using Vox.UI.MenuLogic;
using OpenTK.Graphics.OpenGL;

namespace Vox.UI
{
    public class ImGuiHelper : IImGuiHelper
    {
        private Menu _currentMenu;

        private readonly ImFontPtr _ptFont18;
        private readonly ImFontPtr _ptFont24;
        private readonly ImFontPtr _ptFont72;

        private ITextureLoader? _textureLoader;
        private readonly IAssetLookup? _assetLookup;
        private readonly IssboManager? _ssboManager;
        private readonly IPlayer? _player;
        private readonly IRegionManager? _regionManager;
        private readonly ILightHelper? _lightHelper;
        private readonly IChunkCache? _chunkCache;
        private readonly ISettingsStore? _settings;
        private readonly IShaderManager? _shaderManager;
        private readonly ImGuiIOPtr _ioPtr;
        private readonly ImGuiController _uiController;

        private readonly Action _requestClose;

        // Button and field border outline
        private readonly float _frameBorderSize = 2f;

        // How much darker or lighter to make the frame border than the settings UI Color
        private readonly float _frameBorderColorTint = 0.5f;

        // How much darker or lighter to make the button hover color than the settings UI color
        private readonly float _buttonHoverColorTint = 0.6f;

        // How much darker or lighter to make the field color than the settings UI color
        private readonly float _frameColorTint = 0.9f;

        // How much darker or lighter to make the field when hovered color than the settings UI color
        private readonly float _frameColorHoverTint = 0.95f;

        // How much darker or lighter to make the field when active than the settings UI color
        private readonly float _frameColorActiveTint = 1f;

        // How much darker or lighter to make the button text color than the settings UI color
        private readonly float _buttonTextTint = 2.5f;


        private BlockType _selectedBlock = BlockType.Air;
        private static System.Numerics.Vector3 _pickedColor = System.Numerics.Vector3.Zero;
        private System.Numerics.Vector4 _color = new System.Numerics.Vector4(0,0,0,0.5f);

        public ImGuiHelper(IAssetLookup assetLookup, ITextureLoader textureLoader, IssboManager ssboManager, IPlayer player, IRegionManager regionManager,
            ILightHelper lightHeler, IChunkCache chunkCache, ISettingsStore settings, Action requestClose, IShaderManager shaderManager)
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
            _requestClose = requestClose;
            _shaderManager = shaderManager;

            string assets = Path.Combine("..", "..", "..", "Assets");

            _ptFont18 = _ioPtr.Fonts.AddFontFromFileTTF(Path.Combine(assets, "Grid Hunter.ttf"), 18.0f);
            _ptFont24 = _ioPtr.Fonts.AddFontFromFileTTF(Path.Combine(assets, "Grid Hunter.ttf"), 24.0f);
            _ptFont72 = _ioPtr.Fonts.AddFontFromFileTTF(Path.Combine(assets, "Grid Hunter.ttf"), 72.0f);

        }
        public void CreateMainMenu()
        {
            float guiScale = _settings!.GetSettings().GuiScale;
            System.Numerics.Vector4 uiColor = _settings.GetSettings().UiColor;
            float menuWidth = 20f * guiScale;
            float menuHeight = 15f * guiScale;


            ImGui.PushFont(_ptFont18);

            // Main window UI styling
            ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(uiColor.X * _buttonTextTint, uiColor.Y * _buttonTextTint, uiColor.Z * _buttonTextTint, 1f));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, _frameBorderSize - 1);
            ImGui.PushStyleColor(ImGuiCol.Border, new System.Numerics.Vector4(uiColor.X * _frameBorderColorTint, uiColor.Y * _frameBorderColorTint, uiColor.Z * _frameBorderColorTint, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(uiColor.X * _buttonHoverColorTint, uiColor.Y * _buttonHoverColorTint, uiColor.Z * _buttonHoverColorTint, 1f));
            ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(uiColor.X, uiColor.Y, uiColor.Z, 1f));

            float horizontalMenuScale = 3.5f;
            ImGui.Begin("World List", ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar);

            //Set menu style
            ImGui.PushStyleColor(ImGuiCol.ChildBg, _settings.GetSettings().UiColor);

            ImGui.Text("Choose a World");

                ImGui.BeginChild("World List Pane", new System.Numerics.Vector2(Window.SreenWidth / horizontalMenuScale, menuHeight / 2.5f),
                    ImGuiChildFlags.AlwaysUseWindowPadding | ImGuiChildFlags.AlwaysAutoResize | ImGuiChildFlags.AutoResizeX | ImGuiChildFlags.AutoResizeY);

                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(20f, 2f));
                ImGui.PushStyleVar(ImGuiStyleVar.SeparatorTextPadding, new System.Numerics.Vector2(20f, 2f));
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new System.Numerics.Vector2(10f, 10f));

                //Create UI entries for each world within world list
                try
                {
                    IEnumerable<string> worldList = Directory.EnumerateDirectories(Path.Combine(Window.GetAppFolder(), "worlds"));
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
                                if (folderReverse[i] != Path.DirectorySeparatorChar)
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

                                //Reset the menu chunk coordinates so they render in the world.  
                                foreach (Chunk c in Window.GetMenuChunks())
                                    c.FreeShaderMemory();


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

                if (ImGui.Button("Quit Game##MainMenuQuit"))
                {
                    Console.WriteLine("RequestClose called");
                    _requestClose();
                }

                ImGui.PopStyleVar(3);
                ImGui.EndChild();


            ImGui.PushItemWidth(Window.SreenWidth / horizontalMenuScale);

            ImGui.SetKeyboardFocusHere();
            byte[] buffer = new byte[30];

            ImGui.InputText(" ", buffer, 30);

            Array.Reverse(buffer);
            buffer = [.. buffer.SkipWhile(x => x == 0)];
            string worldName = Encoding.Default.GetString(buffer);

            ImGui.PopItemWidth();


            ImGui.Button("Create New World: " + Encoding.Default.GetString(buffer));
            if (ImGui.IsItemClicked())
            {
                try
                {
                    Directory.CreateDirectory(Path.Combine(Window.GetAppFolder(), "worlds", Encoding.Default.GetString(buffer)));
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                    Logger.Debug(Path.Combine( Window.GetAppFolder(), "worlds", worldName));
                }
            }
            
            ImGui.PopStyleVar();
            ImGui.PopStyleColor(5);
            ImGui.PopFont();
            ImGui.End();
        }
        public void CreateDebugMenu()
        {
            ImGui.PushFont(_ptFont18);

            /*=====================================
             Debug Display
             =====================================*/
            // Menu sizing
            ImGui.SetWindowPos(new System.Numerics.Vector2(0, 0));
            ImGui.SetWindowSize(new System.Numerics.Vector2(Window.SreenWidth / 4.0f, Window.ScreenHeight / 2.0f));

            // Menu styling
            ImGui.PushStyleColor(ImGuiCol.WindowBg, _settings.GetSettings().UiColor);

            ImGui.Begin("Debug");



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
            foreach (KeyValuePair<string, Region> region in _regionManager.GetVisibleRegions())
            {
                if (region.Key == playerRegioIdx)
                    ImGui.Text("Region: " + region.Value.ToString());
            }
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

            ImGui.PopStyleColor();
            ImGui.PopFont();
        }
        public void CreateBlockColorPicker(Vector3 blockspace)
        {
            float guiScale = _settings!.GetSettings().GuiScale;
            float menuWidth = 9.3f * guiScale;
            float menuHeight = 7.2f * guiScale;

            //Menu sizing
            ImGui.SetNextWindowPos(new(Window.SreenWidth / 2, Window.ScreenHeight / 2), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(guiScale * 2.8f, guiScale * 2.8f));

            //Menu styling
            ImGui.PushStyleColor(ImGuiCol.WindowBg, _settings.GetSettings().UiColor);

            if (ImGui.BeginPopup("ColorPicker"))
            {

                ImGui.PushFont(_ptFont18);

                ImGui.PushItemWidth(guiScale * 2.5f);
                if (ImGui.ColorPicker3($"Red: {Math.Round(_pickedColor.X * 15)} Green: {Math.Round(_pickedColor.Y * 15)} Blue: {Math.Round(_pickedColor.Z * 15)}", ref _pickedColor,
                    ImGuiColorEditFlags.InputRGB | ImGuiColorEditFlags.PickerHueWheel | ImGuiColorEditFlags.NoSidePreview | ImGuiColorEditFlags.NoLabel | ImGuiColorEditFlags.DisplayRGB))
                {
                    Chunk blockChunk = _regionManager!.GetAndLoadGlobalChunkFromCoords(blockspace);

                    //Syncronously executes in a separate thread so main thread isnt blocked during propagation and depropagation
                    CountdownEvent countdown = new(1);
                    ThreadPool.QueueUserWorkItem(new WaitCallback(delegate (object state)
                    {

                        _lightHelper!.SetBlockLight(blockspace, new ColorVector(0, 0, 0), blockChunk, true, false);
                        _regionManager.PropagateBlockLight(blockspace, BlockFace.Up, true, true);
                        _regionManager.PropagateBlockLight(blockspace, BlockFace.Down, true, true);
                        _regionManager.PropagateBlockLight(blockspace, BlockFace.East, true, true);
                        _regionManager.PropagateBlockLight(blockspace, BlockFace.West, true, true);
                        _regionManager.PropagateBlockLight(blockspace, BlockFace.North, true, true);
                        _regionManager.PropagateBlockLight(blockspace, BlockFace.South, true, true);
                        _lightHelper.GetLightTrackingList().Remove(blockspace);

                        _regionManager.AddBlockToChunk(
                            blockspace,
                            BlockType.Lamp,
                            new ColorVector(
                                (int)Math.Round(_pickedColor.X * 15),
                                (int)Math.Round(_pickedColor.Y * 15),
                                (int)Math.Round(_pickedColor.Z * 15)
                            ), false
                        );
                        countdown.Signal();
                    }));
                    countdown.Wait();
                }
                ImGui.PopItemWidth();
                ImGui.EndPopup();
                ImGui.PopFont();
            }

            ImGui.PopStyleColor();
        }
        public void CreatePlayerInventory()
        {
            InventoryStore inventoryStore = _player!.GetInventory();
            KeyValuePair<BlockType, int> handItem = inventoryStore.GetHandItem();

            ImGui.PushFont(_ptFont18);
            IntPtr textureToUse = IntPtr.Zero;
            int topFillerPadding = 5;
            int slotPadding = 5;
            float guiScale = _settings!.GetSettings().GuiScale;
            float menuWidth = 9.3f * guiScale;
            float menuHeight = 7.2f * guiScale;
            float slotSize = (menuWidth + slotPadding) / 9;
            float hotbarSpacing = guiScale / 3.5f;
            System.Numerics.Vector2 inventorySlotSize = new(slotSize - (slotPadding * 3f), slotSize - (slotPadding * 3.5f));
            System.Numerics.Vector4 uiColor = _settings.GetSettings().UiColor;

            //Main window sizing and position
            System.Numerics.Vector2 center = ImGui.GetMainViewport().GetCenter();
            ImGui.SetNextWindowPos(new(center.X - (menuWidth / 2), center.Y - (menuHeight / 2)), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new(menuWidth, menuHeight));

            // Main window UI styling
            ImGui.PushStyleColor(ImGuiCol.WindowBg, _settings.GetSettings().UiColor);
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new System.Numerics.Vector2(slotPadding, slotPadding));

            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, _frameBorderSize);
            ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(uiColor.X * _buttonTextTint, uiColor.Y * _buttonTextTint, uiColor.Z * _buttonTextTint, 1f));
            ImGui.PushStyleColor(ImGuiCol.Border, new System.Numerics.Vector4(uiColor.X * _frameBorderColorTint, uiColor.Y * _frameBorderColorTint, uiColor.Z * _frameBorderColorTint, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(uiColor.X * _buttonHoverColorTint, uiColor.Y * _buttonHoverColorTint, uiColor.Z * _buttonHoverColorTint, 1f));
            ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(uiColor.X, uiColor.Y, uiColor.Z, 1f));
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new System.Numerics.Vector4(uiColor.X * _frameColorTint, uiColor.Y * _frameColorTint, uiColor.Z * _frameColorTint, 1f));
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new System.Numerics.Vector4(uiColor.X * _frameColorHoverTint, uiColor.Y * _frameColorHoverTint, uiColor.Z * _frameColorHoverTint, 1f));
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new System.Numerics.Vector4(uiColor.X * _frameColorActiveTint, uiColor.Y * _frameColorActiveTint, uiColor.Z * _frameColorActiveTint, 1f));

            ImGui.Begin("PlayerInventory", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove);

            System.Numerics.Vector2 inventoryFillerSize = new((menuWidth / 3) - (topFillerPadding * 3.5f), guiScale * 2.5f);
                ImGui.BeginChild("InventoryTop", new System.Numerics.Vector2(menuWidth, 2.7f * guiScale));
                ImGui.ImageButton("TopFiller", 0, inventoryFillerSize);
                ImGui.SameLine();
                ImGui.ImageButton("TopFiller", 0, inventoryFillerSize);
                ImGui.SameLine();

                if (_selectedBlock != BlockType.Air && inventoryStore.GetHandItem().Key == BlockType.Air)
                {
                    // Render block model for current slot
                    InventoryStore inventory = _player!.GetInventory();

                    inventory.UpdateSsboBlock(_selectedBlock);

                    RenderInventoryAnimation(256, 256);
                    ImGui.ImageButton("Info Panel", inventoryStore.GetInventoryDisplayTexture(), inventoryFillerSize);
                }
                else
                {
                    //Create empty inventory slot
                    System.Numerics.Vector4 tintCol = new(0, 0, 0, 0);
                    System.Numerics.Vector4 bgCol = new(0, 0, 0, 0);
                    ImGui.ImageButton("##Empty", inventoryStore.GetInventoryDisplayFbo(), inventoryFillerSize, new System.Numerics.Vector2(0, 0), new System.Numerics.Vector2(1, 1), bgCol, tintCol);
                }

                ImGui.EndChild();

            //Populate inventory slots
            Dictionary<int, KeyValuePair<BlockType, int>> inventorySlots = inventoryStore.GetSlots();
            for (int i = 0; i < inventorySlots.Count; i++)
            {
                ImGui.PushID(i);

                BlockType currentSlotType = inventorySlots[i].Key;
                int currentSlotQuantity = inventorySlots[i].Value;

                // Calculate button position and size before drawing
                var cursorPos = ImGui.GetCursorScreenPos();
                var buttonRectMin = cursorPos;
                var buttonRectMax = new System.Numerics.Vector2(cursorPos.X + inventorySlotSize.X, cursorPos.Y + inventorySlotSize.Y);
                var mousePos = ImGui.GetMousePos();
                
                // Determine hover state by manual check *before* drawing
                bool isHovered = mousePos.X >= buttonRectMin.X && mousePos.X <= buttonRectMax.X &&
                                 mousePos.Y >= buttonRectMin.Y && mousePos.Y <= buttonRectMax.Y;
         
                ImDrawListPtr drawList = ImGui.GetWindowDrawList();

                Tuple<System.Numerics.Vector2, System.Numerics.Vector2> slotIconUv = _assetLookup!.GetUvFromBlockType(inventorySlots[i].Key);
            

                // Show animation on hover and set tooltip
                if (isHovered && !inventoryStore.IsItemBeingMoved())
                {
                    _selectedBlock = inventorySlots[i].Key;

                    if (currentSlotType != BlockType.Air)
                    {
                        ImGui.SetTooltip($"{_selectedBlock}\nQuantity: {currentSlotQuantity}");

                        if (inventoryStore.GetHandItem().Key == BlockType.Air)
                            ImGui.ImageButton("##" + i, inventoryStore.GetInventoryDisplayTexture(), inventorySlotSize);
                        else
                            ImGui.ImageButton("##SlotIcon" + i, inventoryStore.GetInventoryIconAtlas(), inventorySlotSize, slotIconUv.Item1, slotIconUv.Item2);

                        if (ImGui.IsItemHovered())
                            inventoryStore.SetHoveredSlotIndex(i);

                    } else
                    {
                        //Create empty inventory slot
                        System.Numerics.Vector4 tintCol = new(0, 0, 0, 0);
                        System.Numerics.Vector4 bgCol = new(0, 0, 0, 0);
                        ImGui.ImageButton("##Empty" + i, inventoryStore.GetInventoryDisplayFbo(), inventorySlotSize, new System.Numerics.Vector2(0, 0), new System.Numerics.Vector2(1, 1), bgCol, tintCol);

                        Utils.DetectItemMove(inventoryStore, inventorySlotSize, drawList, i, currentSlotType, currentSlotQuantity);

                    }
                }
                // Show still icon of 3D block if not hovered
                else
                {

                    if (currentSlotType != BlockType.Air)
                    {
                        ImGui.ImageButton("##SlotIcon" + i, inventoryStore.GetInventoryIconAtlas(), inventorySlotSize, slotIconUv.Item1, slotIconUv.Item2);
                    }

                    //Render an empty slot if theres no item
                    else
                    {
                        //Create empty inventory slot
                        System.Numerics.Vector4 tintCol = new(0, 0, 0, 0);
                        System.Numerics.Vector4 bgCol = new(0, 0, 0, 0);
                        ImGui.ImageButton("##Empty" + i, inventoryStore.GetInventoryDisplayFbo(), inventorySlotSize, new System.Numerics.Vector2(0, 0), new System.Numerics.Vector2(1, 1), bgCol, tintCol);
                        Utils.DetectItemMove(inventoryStore, inventorySlotSize, drawList, i, currentSlotType, currentSlotQuantity);
                    }
                    if (ImGui.IsItemHovered())
                        inventoryStore.SetHoveredSlotIndex(i);
                }

                // Draw item quantity
                System.Numerics.Vector2 min = ImGui.GetItemRectMin();

                uint color = isHovered ? ImGui.GetColorU32(new System.Numerics.Vector4(uiColor.X / _frameColorHoverTint, uiColor.Y / _frameColorHoverTint, uiColor.Z / _frameColorHoverTint, 1)) // if hovered
                                     : ImGui.GetColorU32(new System.Numerics.Vector4(uiColor.X * _buttonHoverColorTint, uiColor.Y * _buttonHoverColorTint, uiColor.Z * _buttonHoverColorTint, 1)); // if not hovered

                // Show item quantity in top left corner
                if (inventorySlots[i].Value > 0)
                {
                    drawList.AddText(ImGui.GetFont(), 0.2f * guiScale, min + new System.Numerics.Vector2(5, 5), color,
                        currentSlotQuantity.ToString());
                }

                Utils.DetectItemMove(inventoryStore, inventorySlotSize, drawList, i, currentSlotType, currentSlotQuantity);

                ImGui.PopID();

                //Splits first 36 slots into rows of 9
                if ((i + 1) % 9 != 0)
                    ImGui.SameLine();

                //Spacing between hotbar and inventory rows                
                if (i == 26)
                    ImGui.Dummy(new(0, hotbarSpacing));
            }

            ImGui.PopStyleVar(3);
            ImGui.PopStyleColor(8);
            ImGui.PopFont();
            ImGui.End();
        }
        public void CreatePauseMenu()
        {
            //Set menu sizing
            float guiScale = _settings!.GetSettings().GuiScale;
            float menuWidth = 9.3f * guiScale;
            float menuHeight = 7.2f * guiScale;

            System.Numerics.Vector4 uiColor = _settings!.GetSettings().UiColor;

            //Main window sizing and position
            System.Numerics.Vector2 center = ImGui.GetMainViewport().GetCenter();
            ImGui.SetNextWindowPos(new(center.X - (menuWidth / 2), center.Y - (menuHeight / 2)), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new(menuWidth, menuHeight));

            // Main window UI styling
            ImGui.PushStyleColor(ImGuiCol.WindowBg, uiColor);
            ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(uiColor.X * _buttonTextTint, uiColor.Y * _buttonTextTint, uiColor.Z * _buttonTextTint, 1f));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, _frameBorderSize);
            ImGui.PushStyleColor(ImGuiCol.Border, new System.Numerics.Vector4(uiColor.X * _frameBorderColorTint, uiColor.Y * _frameBorderColorTint, uiColor.Z * _frameBorderColorTint, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(uiColor.X * _buttonHoverColorTint, uiColor.Y * _buttonHoverColorTint, uiColor.Z * _buttonHoverColorTint, 1f));
            ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(uiColor.X, uiColor.Y, uiColor.Z, 1f));

            // Menu creation
            ImGui.Begin("Paused", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove);

            // Menu title
            ImGui.PushFont(_ptFont72);
            Utils.CreateCenteredText("Paused");
            ImGui.PopFont();

            ImGui.PushFont(_ptFont24);

            // Menu filler
            ImGui.BeginChild("Filler", new(menuWidth, menuHeight / 10));
            ImGui.EndChild();

            if (Utils.ButtonCentered("Settings", guiScale / 2, guiScale / 6))
            {
                _settings.FillBuffersFromSettings();
                SetCurrentMenu(Menu.Settings);
            }
            if (Utils.ButtonCentered("Back To Game", guiScale / 2, guiScale / 6))
            {
                SetCurrentMenu(Menu.None);
            }
            if (Utils.ButtonCentered("Back to Main Menu", guiScale / 2, guiScale / 6))
            {
                SetCurrentMenu(Menu.Main);
                Window.DisplayMainMenuScreen(true);
            }

            ImGui.End();

            // Pop main window UI styling
            ImGui.PopStyleColor(5);
            ImGui.PopStyleVar();
            ImGui.PopFont();
        }
        public void CreateSettingsMenu()
        {

            //Set menu sizing
            float guiScale = _settings!.GetSettings().GuiScale;
            float menuWidth = 9.3f * guiScale;
            float menuHeight = 7.2f * guiScale;

            System.Numerics.Vector4 uiColor = _settings!.GetSettings().UiColor;

            //Main window sizing and position
            System.Numerics.Vector2 center = ImGui.GetMainViewport().GetCenter();
            ImGui.SetNextWindowPos(new(center.X - (menuWidth / 2), center.Y - (menuHeight / 2)), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new(menuWidth, menuHeight));

            // Main window UI styling
            ImGui.PushStyleColor(ImGuiCol.WindowBg, _settings.GetSettings().UiColor);

            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, _frameBorderSize);
            ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(uiColor.X * _buttonTextTint, uiColor.Y * _buttonTextTint, uiColor.Z * _buttonTextTint, 1f));
            ImGui.PushStyleColor(ImGuiCol.Border, new System.Numerics.Vector4(uiColor.X * _frameBorderColorTint, uiColor.Y * _frameBorderColorTint, uiColor.Z * _frameBorderColorTint, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(uiColor.X * _buttonHoverColorTint, uiColor.Y * _buttonHoverColorTint, uiColor.Z * _buttonHoverColorTint, 1f));
            ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(uiColor.X, uiColor.Y, uiColor.Z, 1f));
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new System.Numerics.Vector4(uiColor.X * _frameColorTint, uiColor.Y * _frameColorTint, uiColor.Z * _frameColorTint, 1f));
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new System.Numerics.Vector4(uiColor.X * _frameColorHoverTint, uiColor.Y * _frameColorHoverTint, uiColor.Z * _frameColorHoverTint, 1f));
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new System.Numerics.Vector4(uiColor.X * _frameColorActiveTint, uiColor.Y * _frameColorActiveTint, uiColor.Z * _frameColorActiveTint, 1f));

            // Menu creation
            ImGui.Begin("Settings", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove);

            // Menu title
            ImGui.PushFont(_ptFont72);
            Utils.CreateCenteredText("Settings");
            ImGui.PopFont();

            ImGui.PushFont(_ptFont24);

            // Menu filler
            ImGui.BeginChild("Filler", new(menuWidth, menuHeight / 10));
            ImGui.EndChild();

            float val = _settings.GetSettings().GuiScaleBuffer;
            if (ImGui.SliderFloat("GUI Scale", ref val, 50f, 150f, "%.1f", ImGuiSliderFlags.AlwaysClamp | ImGuiSliderFlags.Logarithmic))
            {
                _settings.GetSettings().GuiScaleBuffer = val;
            }

            ImGui.Dummy(new(guiScale / 2, guiScale / 2));

            ImGui.PushItemWidth(guiScale * 2.5f);
            if (ImGui.ColorPicker4("UI Color", ref _color, ImGuiColorEditFlags.InputRGB | ImGuiColorEditFlags.PickerHueWheel | ImGuiColorEditFlags.DisplayRGB | ImGuiColorEditFlags.NoAlpha)) 
            {
                _settings.GetSettings().UiColorBuffer = new System.Numerics.Vector4(_color.X, _color.Y, _color.Z, 0.6f);
            }
            ImGui.PopItemWidth();

            if (Utils.ButtonCentered("Apply Settings", guiScale / 2, guiScale / 6))
            {
                _settings.SetGuiScale(_settings.GetSettings().GuiScaleBuffer);
                _settings.SetUiColor(_settings.GetSettings().UiColorBuffer);
                _settings.SaveSettingsToFile();
            }
            if (Utils.ButtonCentered("Back", guiScale / 2, guiScale / 6))
            {
                SetCurrentMenu(Menu.Pause);
            }

            ImGui.End();

            // Pop main window UI styling
            ImGui.PopStyleColor(7);
            ImGui.PopStyleVar();
            ImGui.PopFont();
        }
        private void RenderInventoryAnimation(int sizeX, int sizeY)
        {
            InventoryStore inventoryStore = _player!.GetInventory();

            //Save current state
            int prevFbo = GL.GetInteger(GetPName.DrawFramebufferBinding);
            int prevProgram = GL.GetInteger(GetPName.CurrentProgram);
            int prevVao = GL.GetInteger(GetPName.VertexArrayBinding);
            int[] prevViewport = new int[4];
            GL.GetInteger(GetPName.Viewport, prevViewport);

            //FBO setup first, before viewport
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, inventoryStore.GetInventoryDisplayFbo());

            FramebufferStatus status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferStatus.FramebufferComplete)
            {
                Console.WriteLine($"Framebuffer error: {status}");
                return;
            }

            GL.BindVertexArray(inventoryStore.GetInventoryVao());
            GL.Viewport(0, 0, sizeX, sizeY);

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.ClearColor(1, 0, 0, 1);

            //Drawing
            GL.DrawArraysInstanced(
                PrimitiveType.TriangleStrip,  // Drawing a triangle strip
                0,                            // Start from the first vertex in the base geometry
                4,                            // 4 vertices per face (for triangle strip)
                6                             // Instance count (number of faces to draw)
            );



            //Restore state
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, prevFbo);
            GL.UseProgram(prevProgram);
            GL.BindVertexArray(prevVao);
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