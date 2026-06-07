

using System;
using System.Diagnostics;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Vox.Enums;
using Vox.Genesis;
using Vox.UI.MenuLogic;

namespace Vox
{
    public class Utils()
    {
        private const float SQRT2_MINUS_1 = 0.41421356f;
        private const float SQRT3_MINUS_SQRT2 = 0.31783724f;

        public static int FloatCompare(float a, float b)
        {
            // If both numbers are equal, return 0.
            if (a == b)
            {
                return 0;
            }

            // If 'a' is greater than 'b', return 1.
            if (a > b)
            {
                return 1;
            }

            // Otherwise, 'a' is less than 'b', return -1.
            return -1;
        }
        public static string FormatSize(long v)
        {
            if (v < 1024) return v + " B";
            int z = (int)(63 - long.LeadingZeroCount(v)) / 10;
            return string.Format("{0:0.0} {1}B", (double)v / (1L << (z * 10)), " KMGTPE"[z]);
        }
        public static void CopyDirectory(string sourceDir, string destinationDir, bool copySubDirs = true)
        {
            // Get the source directory info
            DirectoryInfo dir = new DirectoryInfo(sourceDir);

            // Check if the source directory exists
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException($"Source directory does not exist: {sourceDir}");
            }

            // If the destination directory doesn't exist, create it
            Directory.CreateDirectory(destinationDir);

            // Copy all files in the directory
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string tempPath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(tempPath, overwrite: true);
            }

            // If copying subdirectories, recursively copy them
            if (copySubDirs)
            {
                DirectoryInfo[] subDirs = dir.GetDirectories();
                foreach (DirectoryInfo subDir in subDirs)
                {
                    string tempPath = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, tempPath, copySubDirs);
                }
            }
        }
        private static readonly Lazy<IEnumerable<Func<long>>> TotalVramUsageCounters = new
        (
            () =>
            {
                if (OperatingSystem.IsWindows())
                {
                    var cat = new PerformanceCounterCategory("GPU Adapter Memory");
                    return [.. cat.GetInstanceNames().SelectMany(cat.GetCounters)
                            .Where(static c => c?.CounterName?.EndsWith("Usage") == true)
                            .Select(static c => new Func<long>(() => c.NextSample().RawValue))];
                }
                else
                {
                    return [];
                }
            },
            LazyThreadSafetyMode.ExecutionAndPublication
        );
        private static readonly Lazy<IEnumerable<Func<long>>> TotalCommittedVram = new
        (
            () =>
            {
                if (OperatingSystem.IsWindows())
                {
                    var cat = new PerformanceCounterCategory("GPU Adapter Memory");
                    return [.. cat.GetInstanceNames().SelectMany(cat.GetCounters)
                            .Where(static c => c?.CounterName?.Equals("Total Committed") == true)
                            .Select(static c => new Func<long>(() => c.NextSample().RawValue))];
                }
                else
                {
                    return [];
                }
            },
            LazyThreadSafetyMode.ExecutionAndPublication
        );
        public static long GetTotalVRamUsage() => TotalVramUsageCounters.Value.Select(x => x()).Sum();
        public static long GetTotalVramCommitted() => TotalCommittedVram.Value.Select(x => x()).Sum();
        public static int GetVectorDistance(Vector3 a, Vector3 b)
        {
            //======================
            //Chevyshev distance
            //return (int) Math.Max(Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)), Math.Abs(a.Z - b.Z));

            //======================
            //Manhattan distance
            //======================
            //return (int)(Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Z - b.Z));

            //======================
            //Euclidean Distantce
            //======================
            // return (int) Vector3.Distance(a, b);

            //===============
            //Octile Distance
            //===============
            float dx = Math.Abs(a.X - b.X);
            float dy = Math.Abs(a.Y - b.Y);
            float dz = Math.Abs(a.Z - b.Z);
            
            float max = Math.Max(dx, Math.Max(dy, dz));
            float min = Math.Min(dx, Math.Min(dy, dz));
            
            // mid approximated as (sum - max - min) but computed implicitly:
            float mid = dx + dy + dz - max - min;
            
            return (int)(max + SQRT2_MINUS_1 * mid + SQRT3_MINUS_SQRT2 * min);

        }
        public static void CreateCenteredText(string text)
        {
            float windowWidth = ImGui.GetWindowSize().X;
            float textWidth = ImGui.CalcTextSize(text).X;

            ImGui.SetCursorPosX((windowWidth - textWidth) * 0.5f);
            ImGui.TextUnformatted(text);
        }
        public static bool ButtonCentered(string label, float paddingX = 20f, float paddingY = 8f)
        {
            System.Numerics.Vector2 textSize = ImGui.CalcTextSize(label);

            System.Numerics.Vector2 buttonSize = new(
                textSize.X + paddingX * 2f,
                textSize.Y + paddingY * 2f
            );

            System.Numerics.Vector2 available = ImGui.GetContentRegionAvail();

            float cursorX = (available.X - buttonSize.X) * 0.5f;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + cursorX);

            return ImGui.Button(label, buttonSize);
        }
        public static void DetectItemDragNDrop(InventoryStore inventoryStore, System.Numerics.Vector2 inventorySlotSize, ImDrawListPtr drawList, int slot, BlockType slotType, int slotQuantity)
        {
            bool isBeingDragged = inventoryStore.IsItemBeingDragged();
            int hoveredSlot = inventoryStore.GetHoveredSlotIndex();
            KeyValuePair<int, KeyValuePair<BlockType, int>> draggedSlot = inventoryStore.GetDraggedSlot();

            /*==================================================================================
            * Detecting item movement from one slot to another with mouse drag and drop
            *==================================================================================*/
            if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                inventoryStore.SetIsItemBeingDragged(false);
                inventoryStore.SetDraggedSlot(new(slot, new(slotType, slotQuantity)));

            }
            // If item is being dragged, draw the display animation as its being dragged
            if (ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            {
                inventoryStore.SetIsItemBeingDragged(true);
                System.Numerics.Vector2 imagePosMin = ImGui.GetMousePos() - new System.Numerics.Vector2(inventorySlotSize.X / 2f, inventorySlotSize.Y / 2f);
                System.Numerics.Vector2 imagePosMax = imagePosMin + inventorySlotSize;

                if (draggedSlot.Value.Key != BlockType.AIR)
                    drawList.AddImage(inventoryStore.GetInventoryDisplayTexture(), imagePosMin, imagePosMax);
            }
            // Move the item to another slot
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left) && hoveredSlot != -1 && isBeingDragged)
            {
                // Make sure the slot does not alreay have an item in it
                if (inventoryStore.GetSlots()[hoveredSlot].Key == BlockType.AIR)
                {
                    inventoryStore.SetSlot(hoveredSlot, draggedSlot.Value.Key, draggedSlot.Value.Value);
                    inventoryStore.SetSlot(inventoryStore.GetDraggedSlot().Key, BlockType.AIR, 0);

                    inventoryStore.SetDraggedSlot(new(-1, new(BlockType.AIR, 0)));
                    inventoryStore.SetIsItemBeingDragged(false);
                }
            }
        }

    }
}
