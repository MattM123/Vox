using System.Diagnostics;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using Vox.AssetManagement;
using Vox.Enums;
using Vox.Genesis;
using Vox.Model;
using Vox.Rendering;

namespace Vox.UI
{
    public static class ImGuiHelper
    {
        private static int rotationAngle = 0;
        public static bool SHOW_BLOCK_COLOR_PICKER = false;
        public static bool SHOW_PLAYER_INVENTORY = false;

        public static int _inventoryIconFBO;
        public static int _inventoryIconTexture;

        private static System.Numerics.Vector3 pickedColor = System.Numerics.Vector3.Zero; 
        public static void ShowWorldMenu(ImGuiIOPtr ioptr)
        {
            float horizontalMenuScale = 3.5f;
            ImGui.Begin("World List", ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar);

            //Set menu style
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new System.Numerics.Vector4(0f, 0f, 0f, 0.3f));
            //Set menu size

            ImGui.SetWindowSize(new System.Numerics.Vector2(Window.screenWidth - 400 / horizontalMenuScale, Window.screenHeight));
            ImGui.SetWindowPos(new System.Numerics.Vector2(Window.screenWidth / 30, Window.screenHeight / 40));

            //  ImGui.PushFont(font);       
            ImGui.Text("Choose a World");
            //  ImGui.PopFont();

                ImGui.BeginChild("World List Pane", new System.Numerics.Vector2(Window.screenWidth / horizontalMenuScale, Window.screenHeight / 1.15f),
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
                                //Reset the menu chunk coordinates so the render in the world.  
                                foreach (Chunk c in Window.GetMenuChunks())
                                    c.Reset();


                                Window.SetMenuRendered(false);
                                RegionManager rm = new(folder);
                                Window.SetLoadedWorld(rm);
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
                    ImGui.Text("FPS: " + ioptr.Framerate);
                }
                catch (Exception e2)
                {
                    Logger.Error(e2);

                }

                ImGui.PopStyleVar(2);
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

            ImGui.PopStyleVar(4);
            ImGui.PopStyleColor();
            ImGui.End();
        }

        public static void ShowDebugMenu(ImGuiIOPtr ioptr)
        {
            /*=====================================
             Debug Display
             =====================================*/
            ImGui.Begin("Debug");

            ImGui.SetWindowPos(new System.Numerics.Vector2(0, 0));
            ImGui.SetWindowSize(new System.Numerics.Vector2(Window.screenWidth / 4.0f, Window.screenHeight / 2.0f));

            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f)));
            ImGui.Text("Player");
            ImGui.PopStyleColor();

            ImGui.Text($"Position: X:{Window.GetPlayer().GetPosition().X} Y:{Window.GetPlayer().GetPosition().Y} Z:{Window.GetPlayer().GetPosition().Z}");
            ImGui.Text($"Rotation: X:{Window.GetPlayer().GetRotation().X}, Y:{Window.GetPlayer().GetRotation().Y}");
            ImGui.Text("IsGrounded: " + Window.GetPlayer().IsPlayerGrounded());

            BlockFace f = BlockFace.ALL;
            OpenTK.Mathematics.Vector3 block = Window.GetPlayer().UpdateViewTarget(out f, out _, out OpenTK.Mathematics.Vector3 blockSpace);
            Chunk blockChunk = RegionManager.GetAndLoadGlobalChunkFromCoords(block);
            OpenTK.Mathematics.Vector3i idx = RegionManager.GetChunkRelativeCoordinates(block);
            BlockType type = (BlockType) blockChunk.blockData[idx.X, idx.Y, idx.Z];
            ImGui.Text($"Looking At: {blockSpace} ({type})");
            ImGui.Text("");

            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f)));
            ImGui.Text("World");
            ImGui.PopStyleColor();

            ImGui.Text("Region: " + Window.GetPlayer().GetRegionWithPlayer().ToString());
            ImGui.Text(Window.GetPlayer().GetChunkWithPlayer().ToString());
            ImGui.Text("Chunks Surrounding Player: " + ChunkCache.UpdateChunkCache().Count);

            ImGui.Text("");
            ImGui.Text("Chunks In Memory: " + RegionManager.PollChunkMemory());
            string str = "Regions In Memory:\n";
            foreach (KeyValuePair<string, Region> r in ChunkCache.GetRegions())
                str += $"[{r.Key}] {r.Value}\n";

            ImGui.Text(str);
            ImGui.Text("");

            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f)));
            ImGui.Text("Performance");
            ImGui.PopStyleColor();

            ImGui.Text("FPS: " + ioptr.Framerate);
            ImGui.Text("Memory: " + Utils.FormatSize(Process.GetCurrentProcess().WorkingSet64) + "/" + Utils.FormatSize(Process.GetCurrentProcess().PrivateMemorySize64));
            ImGui.Text("VRAM: " + Utils.FormatSize(Utils.GetTotalVRamUsage()) + "/" + Utils.FormatSize(Utils.GetTotalVramCommitted()));
            ImGui.Text("");

            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f)));
            ImGui.Text("Player Matrix");
            ImGui.PopStyleColor();

            ImGui.Text(Window.GetPlayer().GetViewMatrix().ToString());
            ImGui.End();
        }

        public static void ShowBlockColorPicker(OpenTK.Mathematics.Vector3 blockspace) {
            ImGui.SetNextWindowPos(new(Window.screenWidth / 2, Window.screenHeight / 2), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(410, 270));

            if (ImGui.BeginPopup("ColorPicker"))
            {
                if (ImGui.ColorPicker3($"Red: {Math.Round(pickedColor.X * 15)} Green: {Math.Round(pickedColor.Y * 15)} Blue: {Math.Round(pickedColor.Z * 15)}", ref pickedColor))
                {
                    Chunk blockChunk = RegionManager.GetAndLoadGlobalChunkFromCoords(blockspace);

                    //Syncronously executes in a separate thread so main thread isnt blocked during propagation and depropagation
                    CountdownEvent countdown = new(1);
                    ThreadPool.QueueUserWorkItem(new WaitCallback(delegate (object state)
                    {

                        LightHelper.SetBlockLight(blockspace, new ColorVector(0, 0, 0), blockChunk, true, false);
                        LightHelper.PropagateBlockLight(blockspace, BlockFace.UP, true, true);
                        LightHelper.PropagateBlockLight(blockspace, BlockFace.DOWN, true, true);
                        LightHelper.PropagateBlockLight(blockspace, BlockFace.EAST, true, true);
                        LightHelper.PropagateBlockLight(blockspace, BlockFace.WEST, true, true);
                        LightHelper.PropagateBlockLight(blockspace, BlockFace.NORTH, true, true);
                        LightHelper.PropagateBlockLight(blockspace, BlockFace.SOUTH, true, true);
                        LightHelper.GetLightTrackingList().Remove(blockspace);

                        RegionManager.AddBlockToChunk(
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
            }
        }


        private static BlockType selectedBlock = BlockType.AIR;
        public static void ShowPlayerInventory(ImGuiController controller)
        {
            rotationAngle += 1;
            if (rotationAngle > 360)
                rotationAngle = 0;

            //Main window sizing and position
            System.Numerics.Vector2 center = ImGui.GetMainViewport().GetCenter();
            ImGui.SetNextWindowPos(new(center.X / 2.2f, center.Y / 4f), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new(Window.screenWidth / 1.8f, Window.screenHeight / 1.3f));
            

            ImGui.Begin("PlayerInventory", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove);

            ImGui.BeginChild("InventoryTop", new System.Numerics.Vector2(0, Window.screenHeight / 3.25f));
            ImGui.ImageButton("TopFiller", 0, new System.Numerics.Vector2(Window.screenWidth / 5.7f, Window.screenHeight / 3.35f));
            ImGui.SameLine();
            ImGui.ImageButton("TopFiller", 0, new System.Numerics.Vector2(Window.screenWidth / 5.7f, Window.screenHeight / 3.35f));
            ImGui.SameLine();

            if (selectedBlock != BlockType.AIR)
            {
                // Render block model for current slot
                InventoryStore inventory = Window.GetPlayer().GetInventory();
                BlockModel model = ModelLoader.GetModel(selectedBlock);
                string modelElements = model.GetElements().ElementAt(0).ToString();

                int start = modelElements.IndexOf("from=(") + 6;
                int end = modelElements.IndexOf(")", start);

                string coords = modelElements[start..end];
                string[] parts = coords.Split(',');


                int x = int.Parse(parts[0].Trim());
                int y = int.Parse(parts[1].Trim());
                int z = int.Parse(parts[2].Trim());

//                   Console.WriteLine($"Parsed coordinates: x={x}, y={y}, z={z}");
                
                inventory.AddOrUpdateFaceInMemory(
                    new BlockFaceInstance
                    {
                        facePosition = new OpenTK.Mathematics.Vector3(x, y, z + 1),
                        index = 0,
                        lighting = 0,
                        textureLayer = (int)model.GetTexture(BlockFace.NORTH),
                        faceDirection = (int)BlockFace.NORTH
                    });
                inventory.AddOrUpdateFaceInMemory(
                    new BlockFaceInstance
                    {
                        facePosition = new OpenTK.Mathematics.Vector3(x, y, z - 1),
                        index = 1,
                        lighting = 0,
                        textureLayer = (int)model.GetTexture(BlockFace.SOUTH),
                        faceDirection = (int)BlockFace.SOUTH
                    });
                inventory.AddOrUpdateFaceInMemory(
                    new BlockFaceInstance
                    {
                        facePosition = new OpenTK.Mathematics.Vector3(x + 1, y, z),
                        index = 2,
                        lighting = 0,
                        textureLayer = (int)model.GetTexture(BlockFace.EAST),
                        faceDirection = (int)BlockFace.EAST
                    });
                inventory.AddOrUpdateFaceInMemory(
                    new BlockFaceInstance
                    {
                        facePosition = new OpenTK.Mathematics.Vector3(x - 1, y, z),
                        index = 3,
                        lighting = 0,
                        textureLayer = (int)model.GetTexture(BlockFace.WEST),
                        faceDirection = (int)BlockFace.WEST
                    });
                inventory.AddOrUpdateFaceInMemory(
                    new BlockFaceInstance
                    {
                        facePosition = new OpenTK.Mathematics.Vector3(x, y + 1, z),
                        index = 4,
                        lighting = 0,
                        textureLayer = (int)model.GetTexture(BlockFace.UP),
                        faceDirection = (int)BlockFace.UP
                    });
                inventory.AddOrUpdateFaceInMemory(
                    new BlockFaceInstance
                    {
                        facePosition = new OpenTK.Mathematics.Vector3(x, y - 1, z),
                        index = 5,
                        lighting = 0,
                        textureLayer = (int)model.GetTexture(BlockFace.DOWN),
                        faceDirection = (int)BlockFace.DOWN
                    });

                RenderInventoryAnimation(256, 256);
                ImGui.ImageButton("Info Panel", _inventoryIconTexture, new System.Numerics.Vector2(Window.screenWidth / 5.7f, Window.screenHeight / 3.35f));
            } else
            {
                ImGui.ImageButton("Info Panel", 0, new System.Numerics.Vector2(Window.screenWidth / 5.7f, Window.screenHeight / 3.35f));
            }

            ImGui.EndChild();

            //Inventory UI Stying
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 8f);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0.5f);
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new System.Numerics.Vector2(1, 1));
            ImGui.PushStyleColor(ImGuiCol.Border, new System.Numerics.Vector4(0.05f, 0.05f, 0.05f, 1f));
            ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.15f, 0.15f, 0.15f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0.3f, 0.3f, 0.3f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new System.Numerics.Vector4(0.35f, 0.35f, 0.35f, 1f));


            //Populate inventory slots
            Dictionary<int, KeyValuePair<BlockType, int>> inventorySlots = Window.GetPlayer().GetInventory().GetSlots();
            for (int i = 0; i < inventorySlots.Count; i++)
            {
                ImGui.PushID(i);
                BlockType currentSlotType = inventorySlots[i].Key;

                if (currentSlotType != BlockType.AIR)
                {

                    System.Numerics.Vector2 size = new((ImGui.GetWindowSize().X / 9f) - 10.5f, 64);


                    // Calculate button position and size before drawing
                    var cursorPos = ImGui.GetCursorScreenPos();
                    var buttonRectMin = cursorPos;
                    var buttonRectMax = new System.Numerics.Vector2(cursorPos.X + size.X, cursorPos.Y + size.Y);
                    var mousePos = ImGui.GetMousePos();

                    // Determine hover state by manual check *before* drawing
                    bool isHovered = mousePos.X >= buttonRectMin.X && mousePos.X <= buttonRectMax.X &&
                                     mousePos.Y >= buttonRectMin.Y && mousePos.Y <= buttonRectMax.Y;

                    // Select texture image for slot
                    IntPtr textureToUse = TextureLoader.LoadSingleTexture(AssetLookup.BlockTypeToIconFile[currentSlotType]);

                    if (isHovered)
                    {
                        selectedBlock = inventorySlots[i].Key;
                        ImGui.SetTooltip($"{selectedBlock}\nQuantity: {inventorySlots[i].Value}");
                    }

                    ImGui.ImageButton("##" + i, textureToUse, size);

                    // Draw rectangle around the button
                    System.Numerics.Vector2 min = ImGui.GetItemRectMin();
                    System.Numerics.Vector2 max = ImGui.GetItemRectMax();

                    var drawList = ImGui.GetWindowDrawList();

                    uint color = isHovered ? ImGui.GetColorU32(new System.Numerics.Vector4(0, 0, 1, 1)) // blue if hovered
                                         : ImGui.GetColorU32(new System.Numerics.Vector4(1, 1, 1, 1)); // white if not hovered

                    drawList.AddRect(min, max, color, rounding: 3.0f, ImDrawFlags.None, thickness: 2.0f);

                    // Show item quantity in top left corner
                    drawList.AddText(ImGui.GetFont(), 15, min + new System.Numerics.Vector2(5, 5), color,
                        Window.GetPlayer().GetInventory().GetSlots()[i].Value.ToString());

                }
                else
                {
                    //Create ImageButtom
                    ImGui.ImageButton("##" + i, 0, new((ImGui.GetWindowSize().X / 9f) - 10.5f, 64));

                }
                ImGui.PopID();

                //Splits first 36 slots into rows of 9
                if ((i + 1) % 9 != 0)
                    ImGui.SameLine();

                //Spacing between hotbar and inventory rows                
                if (i == 26)
                    ImGui.Dummy(new(0, 15));


            }

            ImGui.PopStyleVar(3);
            ImGui.PopStyleColor(4);
            ImGui.End();
        }

        public static bool IsAnyMenuActive()
        {
            return SHOW_BLOCK_COLOR_PICKER || SHOW_PLAYER_INVENTORY;
        }

        private static int _inventoryVAO = GL.GenVertexArray();
        private static void RenderInventoryAnimation(int sizeX, int sizeY)
        {
            Window.SetTerrainShaderUniforms();

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

            // Setup shader and viewport
            Window.shaderManager.GetShaderProgram("Inventory").Bind();

            GL.BindVertexArray(_inventoryVAO);  // Bind the VAO with dummy VBO
            GL.Viewport(0, 0, sizeX, sizeY);

            // Bind SSBO for vertex data
            var inventorySsbo = Window.ssboManager.GetSSBO("Inventory");
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, inventorySsbo.Handle);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, inventorySsbo.BindingIndex, inventorySsbo.Handle);


            //Drawing
            GL.DrawArraysInstanced(
                PrimitiveType.TriangleStrip,  // Drawing a triangle strip
                0,                            // Start from the first vertex in the base geometry
                4,                            // 4 vertices per face (for triangle strip)
                6                             // Instance count (number of faces to draw)
            );

            GL.ClearColor(0, 0, 0, 0);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            //Restore state
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, prevFBO);
            GL.UseProgram(prevProgram);
            GL.BindVertexArray(prevVAO);
            GL.Viewport(prevViewport[0], prevViewport[1], prevViewport[2], prevViewport[3]);


        }
    }
}
