using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Text;
using ImGuiNET;
using Vox.Enums;
using Vox.Genesis;
using Vox.Rendering;

namespace Vox.UI
{
    public static class ImGuiHelper
    {
        public static bool SHOW_BLOCK_COLOR_PICKER = false;
        private static Vector3 pickedColor = Vector3.Zero; 
        public static void ShowWorldMenu(ImGuiIOPtr ioptr)
        {
            float horizontalMenuScale = 3.5f;
            ImGui.Begin("World List", ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar);

            //Set menu style
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0f, 0f, 0f, 0.3f));
            //Set menu size

            ImGui.SetWindowSize(new Vector2(Window.screenWidth - 400 / horizontalMenuScale, Window.screenHeight));
            ImGui.SetWindowPos(new Vector2(Window.screenWidth / 30, Window.screenHeight / 40));

            //  ImGui.PushFont(font);       
            ImGui.Text("Choose a World");
            //  ImGui.PopFont();

            ImGui.BeginChild("World List Pane", new Vector2(Window.screenWidth / horizontalMenuScale, Window.screenHeight / 1.15f),
                ImGuiChildFlags.AlwaysUseWindowPadding | ImGuiChildFlags.AlwaysAutoResize);

            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(20f, 2f));
            ImGui.PushStyleVar(ImGuiStyleVar.SeparatorTextPadding, new Vector2(20f, 2f));
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(10f, 10f));

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

            ImGui.PopStyleVar();
            ImGui.PopStyleVar();
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

            ImGui.PopStyleVar();
            ImGui.PopStyleVar();
            ImGui.PopStyleVar();
            ImGui.PopStyleVar();
            ImGui.PopStyleColor();
            ImGui.End();
        }

        public static void ShowDebugMenu(ImGuiIOPtr ioptr)
        {
            /*=====================================
             Debug Display
             =====================================*/
            ImGui.Begin("Debug");

            ImGui.SetWindowPos(new Vector2(0, 0));
            ImGui.SetWindowSize(new Vector2(Window.screenWidth / 4.0f, Window.screenHeight / 2.0f));

            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 1.0f, 0.0f, 1.0f)));
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

            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 1.0f, 0.0f, 1.0f)));
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
            ImGui.SetNextWindowSize(new Vector2(400, 350));
            ImGui.Begin($"Color at {blockspace}: Red: {Math.Round(pickedColor.X * 15)} Green: {Math.Round(pickedColor.Y * 15)} Blue: {Math.Round(pickedColor.Z * 15)} ");

            if (ImGui.ColorPicker3($"Picked Color", ref pickedColor))
            {
                Chunk blockChunk = RegionManager.GetAndLoadGlobalChunkFromCoords(blockspace);

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
            ImGui.End();
        }
    }
}
