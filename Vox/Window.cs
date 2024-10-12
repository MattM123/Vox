using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text;
using ImGuiNET;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Vox.Genesis;
using Vox.GUI;
using Vox.Model;
using Vox.Texturing;
using BufferTarget = OpenTK.Graphics.OpenGL4.BufferTarget;
using BufferUsageHint = OpenTK.Graphics.OpenGL4.BufferUsageHint;
using ClearBufferMask = OpenTK.Graphics.OpenGL4.ClearBufferMask;
using DrawElementsType = OpenTK.Graphics.OpenGL4.DrawElementsType;
using EnableCap = OpenTK.Graphics.OpenGL4.EnableCap;
using GL = OpenTK.Graphics.OpenGL4.GL;
using PrimitiveType = OpenTK.Graphics.OpenGL4.PrimitiveType;
using StringName = OpenTK.Graphics.OpenGL4.StringName;
using Vector3 = OpenTK.Mathematics.Vector3;
using VertexAttribPointerType = OpenTK.Graphics.OpenGL4.VertexAttribPointerType;


namespace Vox
{
    public class Window(GameWindowSettings windowSettings, NativeWindowSettings nativeSettings) : GameWindow(windowSettings, nativeSettings)
    {
        ImGuiController _controller;
        public static readonly int screenWidth = Monitors.GetPrimaryMonitor().ClientArea.Size.X;
        public static readonly int screenHeight = Monitors.GetPrimaryMonitor().ClientArea.Size.Y - 100;

        private static bool renderMenu = true;
        private static readonly string appFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\.voxelGame\\";
        private static ShaderProgram? shaders = null;
        private static Player? player = null;
        private readonly float FOV = MathHelper.DegreesToRadians(50.0f);
        private static RegionManager? loadedWorld;
        private static float angle = 0.0f;
        private static Chunk? globalPlayerChunk = null;
        private static Region? globalPlayerRegion = null;
        private static Matrix4 modelMatricRotate;
        private static Matrix4 viewMatrix;
        private static float fps = 0.0f;
        public static string assets = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\.voxelGame\\Assets\\";
        private static BlockModel menuModel;
        private static Chunk c;
        private float mouseSensitivity = 1.0f;
        private static int vbo, ebo, vao = 0;
        private static Thread mainThread;
        private static ConcurrentQueue<Action> mainThreadQueue;

        protected override void OnLoad()
        {
            base.OnLoad();
            mainThread = Thread.CurrentThread;
            mainThreadQueue = new ConcurrentQueue<Action>();

            //Mene render buffers
            vbo = GL.GenBuffer();
            ebo = GL.GenBuffer();
            vao = GL.GenVertexArray();

            //Load textures and models
            TextureLoader.LoadTextures();
            ModelLoader.LoadModels();
            c = new Chunk().Initialize(0, 0);
            c.GetRenderTask();

            Title += ": OpenGL Version: " + GL.GetString(StringName.Version);

            _controller = new ImGuiController(ClientSize.X, ClientSize.Y);
            shaders = new();

            Directory.CreateDirectory(appFolder + "worlds");
            GL.Enable(EnableCap.DepthTest);
            

            //Enable primitive restart
            GL.Enable(EnableCap.PrimitiveRestart);
            GL.PrimitiveRestartIndex(80000);

            //========================
            //Shader Compilation
            //========================

            //Load the vertex shader from file
            string vertexShaderSource = ShaderProgram.LoadShaderFromFile("..\\..\\..\\Texturing\\VertexShader.glsl");
            shaders.CreateVertexShader(vertexShaderSource);

            // Load the fragment shader from file
            string fragmentShaderSource = ShaderProgram.LoadShaderFromFile("..\\..\\..\\Texturing\\FragShader.glsl");
            shaders.CreateFragmentShader(fragmentShaderSource);

            //Load Textures
            shaders.UploadTexture("texture_sampler", 0);

            // Link the shader program
            shaders.Link();

            // Use shader for rendering
            shaders.Bind();

            //========================
            //Matrix setup
            //========================

            float FAR = 500.0f;
            float NEAR = 0.01f;
            Matrix4 pMatrix = Matrix4.CreatePerspectiveFieldOfView(FOV, (float)ClientSize.X / ClientSize.Y, NEAR, FAR);

            viewMatrix = Matrix4.LookAt(new Vector3(-10f, 220f, -10f), new Vector3(8f, 200f, 8f), new Vector3(0.0f, 1f, 0.0f));

            shaders.CreateUniform("projectionMatrix");
            shaders.SetMatrixUniform("projectionMatrix", pMatrix);

            shaders.CreateUniform("modelMatrix");
            shaders.SetMatrixUniform("modelMatrix", modelMatricRotate);

            shaders.CreateUniform("viewMatrix");
            shaders.SetMatrixUniform("viewMatrix", viewMatrix);

            shaders.CreateUniform("chunkModelMatrix");
            shaders.SetMatrixUniform("chunkModelMatrix", Chunk.GetModelMatrix());

            shaders.CreateUniform("isMenuRendered");
            shaders.SetIntUniform("isMenuRendered", 1);
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            //Process main thread queue prior to rendering each frame
            if (!mainThreadQueue.IsEmpty)
            {
                foreach (Action action in mainThreadQueue)
                    action();
            }

            /*==============================
            Update UI input and config
            ===============================*/
            _controller.Update(this, (float)e.Time);

            GL.ClearColor(new Color4(45, 88, 48, 255));
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);


            /*==================================
            World and Menu rendering
            ====================================*/
            if (loadedWorld == null)
            {
                if (angle > 360)
                    angle = 0.0f;

                angle += 0.0001f;
                renderMenu = true;
                shaders?.SetIntUniform("isMenuRendered", 1);
                RenderMenu();
            }
            else
            {
                renderMenu = false;
                shaders?.SetIntUniform("isMenuRendered", 0);
                RenderWorld();
           
            }

            ImGuiController.CheckGLError("End of frame");

            /*======================================
            Render new UI frame over everything
            ========================================*/
            RenderUI();
            _controller.Render();

            SwapBuffers();


        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);

            if (IsMenuRendered())
            {
                modelMatricRotate = Matrix4.CreateFromAxisAngle(new Vector3(100f, 2000, 100f), angle);
                Matrix4 menuMatrix = modelMatricRotate;
                shaders?.SetMatrixUniform("modelMatrix", menuMatrix);
            } else
            {
                player.SetLookDir(MouseState.Y, MouseState.X);
                shaders.SetMatrixUniform("viewMatrix", player.GetViewMatrix());

            }

        }

        protected override void OnKeyDown(KeyboardKeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (!IsMenuRendered()) {
                if (e.Key == Keys.W)
                    player.MoveForward(1 * mouseSensitivity);
                if (e.Key == Keys.S)
                    player.MoveBackwards(1 * mouseSensitivity);
                if (e.Key == Keys.A)
                    player.MoveLeft(1 * mouseSensitivity);
                if (e.Key == Keys.D)
                    player.MoveRight(1 * mouseSensitivity);
                if (e.Key == Keys.LeftShift)
                    player.MoveDown(1);
                if (e.Key == Keys.Space)
                    player.MoveUp(1);
            }

        }
        protected override void OnKeyUp(KeyboardKeyEventArgs e)
        {
            base.OnKeyUp(e);
        }

        protected override void OnMouseMove(MouseMoveEventArgs e)
        {
            base.OnMouseMove(e);
        }

        protected override void OnTextInput(TextInputEventArgs e)
        {
            base.OnTextInput(e);
            _controller.PressChar((char)e.Unicode);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);

            _controller.MouseScroll(e.Offset);
        }

        private void RenderUI()
        {
            ImGuiIOPtr ioptr = ImGui.GetIO();
            float horizontalMenuScale = 3.5f;
            fps = ioptr.Framerate;

            if (renderMenu)
            {
                ImGui.Begin("World List", ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar);

                //Set menu style
                ImGui.PushStyleColor(ImGuiCol.ChildBg, new System.Numerics.Vector4(0f, 0f, 0f, 0.3f));
                //Set menu size

                ImGui.SetWindowSize(new System.Numerics.Vector2(screenWidth - 400 / horizontalMenuScale, screenHeight));
                ImGui.SetWindowPos(new System.Numerics.Vector2(screenWidth / 30, screenHeight / 40));

                //  ImGui.PushFont(font);       
                ImGui.Text("Choose a World");
                //  ImGui.PopFont();

                ImGui.BeginChild("World List Pane", new System.Numerics.Vector2(screenWidth / horizontalMenuScale, screenHeight / 1.15f),
                    ImGuiChildFlags.AlwaysUseWindowPadding | ImGuiChildFlags.AlwaysAutoResize);

                ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(20f, 2f));
                ImGui.PushStyleVar(ImGuiStyleVar.SeparatorTextPadding, new System.Numerics.Vector2(20f, 2f));
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new System.Numerics.Vector2(10f, 10f));

                //Create UI entries for each world within world list
                try
                {
                    IEnumerable<string> worldList = Directory.EnumerateDirectories(appFolder + "worlds");
                    int dirLen = worldList.Count();

                    if (dirLen > 0)
                    {
                        foreach (string folder in worldList)
                        {

                            //World label                  
                            string label = "";
                            string folderReverse = new(folder.Reverse().ToArray());

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
                               
                                renderMenu = false;
                                RegionManager rm = new(folder);
                                loadedWorld = rm;
                                player = new Player();
                            }
                            ImGui.SameLine();

                            //Delete button
                            ImGui.Button("Delete World");
                            if (ImGui.IsItemClicked())
                            {
                                try
                                {
                                    Directory.Delete(folder);
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

                ImGui.PushItemWidth(screenWidth / horizontalMenuScale);

                ImGui.SetKeyboardFocusHere();
                byte[] buffer = new byte[30];

                ImGui.InputText(" ", buffer, 30);

                buffer = buffer.Reverse().ToArray();
                buffer = buffer.SkipWhile(x => x == 0).ToArray();
                string worldName = Encoding.Default.GetString(buffer);

                ImGui.PopItemWidth();


                ImGui.Button("Create New World: " + Encoding.Default.GetString(buffer.Reverse().ToArray()));
                if (ImGui.IsItemClicked())
                {
                    try
                    {
                        Directory.CreateDirectory(appFolder + "worlds\\" + Encoding.Default.GetString(buffer.Reverse().ToArray()));
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e);
                        Logger.Debug(appFolder + "worlds\\" + worldName);
                    }
                }

                ImGui.PopStyleVar();
                ImGui.PopStyleVar();
                ImGui.PopStyleVar();
                ImGui.PopStyleVar();
                ImGui.PopStyleColor();
                ImGui.End();
            }
            else
            {
                /*=====================================
                Debug Display
                =====================================*/
                ImGui.Begin("Debug");

                ImGui.SetWindowPos(new System.Numerics.Vector2(0, 0));
                ImGui.SetWindowSize(new System.Numerics.Vector2(screenWidth / 4.0f, screenHeight / 2.0f));

                ImGui.PushStyleColor(ImGuiCol.Text, ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f)));
                ImGui.Text("Player");
                ImGui.PopStyleColor();

                ImGui.Text("Position: X:" + GetPlayer().GetPosition().X + " Y:" + GetPlayer().GetPosition().Y + " Z:" + GetPlayer().GetPosition().Z);
                ImGui.Text("Rotation: X:" + GetPlayer().GetRotation().X + ", Y:" + GetPlayer().GetRotation().Y);
                ImGui.Text("Cursor Position: " + Cursor.X + ", " + Cursor.Y);
                ImGui.Text("");

                ImGui.PushStyleColor(ImGuiCol.Text, ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f)));
                ImGui.Text("World");
                ImGui.PopStyleColor();

                ImGui.Text("Region: " + GetPlayer().GetRegionWithPlayer().ToString());
                ImGui.Text(GetPlayer().GetChunkWithPlayer().ToString());
                ImGui.Text("Chunk Cache Size: " + (RegionManager.RENDER_DISTANCE + RegionManager.RENDER_DISTANCE + 1)
                    * (RegionManager.RENDER_DISTANCE + RegionManager.RENDER_DISTANCE + 1));

                string str = "Visible Regions:\n";
                foreach (Region r in ChunkCache.GetRegions())
                    str += r.ToString() + "\n";

                ImGui.Text(str);
                ImGui.Text("");

                ImGui.PushStyleColor(ImGuiCol.Text, ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f)));
                ImGui.Text("Performance");
                ImGui.PopStyleColor();

                ImGui.Text("FPS: " + fps);
                ImGui.Text("Memory: " + Utils.FormatSize(GC.GetTotalMemory(false)) + "/" + Utils.FormatSize(GC.GetTotalMemory(true)));
                ImGui.Text("");

                ImGui.PushStyleColor(ImGuiCol.Text, ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f)));
                ImGui.Text("Player Matrix");
                ImGui.PopStyleColor();

                ImGui.Text(player.GetViewMatrix().ToString());
                ImGui.End();
            }
        }

        public static bool IsMenuRendered() { return renderMenu; }

        private static void RenderMenu()
        {
            /*==================================
            Buffer binding and loading
            ====================================*/

            GL.BindVertexArray(vao);

            //Vertices
            float[] vertexBuffer = c.GetVertices();

            // Create VBO upload the vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertexBuffer.Length * sizeof(float), vertexBuffer, BufferUsageHint.StaticDraw);

            //Elements
            int[] elementBuffer = c.GetElements();

            // Create EBO upload the element buffer;
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, elementBuffer.Length * sizeof(int), elementBuffer, BufferUsageHint.StaticDraw);

            float[] ints = new float[vertexBuffer.Length];
            GL.GetBufferSubData(BufferTarget.ArrayBuffer, 0, vertexBuffer.Length, ints);

            /*=====================================
            Vertex attribute definitions for shaders
            ======================================*/
            int posSize = 3;
            int layerSize = 1;
            int coordSize = 1;
            int floatSizeBytes = 4;
            int vertexSizeBytes = (posSize + layerSize + coordSize) * floatSizeBytes;

            // Position
            GL.VertexAttribPointer(0, posSize, VertexAttribPointerType.Float, false, vertexSizeBytes, 0);
            GL.EnableVertexAttribArray(0);

            // Texture Layer
            GL.VertexAttribPointer(1, layerSize, VertexAttribPointerType.Float, false, vertexSizeBytes, (posSize) * sizeof(float));
            GL.EnableVertexAttribArray(1);

            // Texture Coordinates
            GL.VertexAttribPointer(2, coordSize, VertexAttribPointerType.Float, false, vertexSizeBytes, (posSize + layerSize) * sizeof(float));
            GL.EnableVertexAttribArray(2);

            /*==================================
            Drawing
            ====================================*/
            GL.DrawElements(PrimitiveType.TriangleStrip, elementBuffer.Length, DrawElementsType.UnsignedInt, 0);

            //Unbind and cleanup everything
            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
            GL.DisableVertexAttribArray(2);

            // Delete VAO, VBO, and EBO
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            
        }


        private void RenderWorld()
        {

            /*====================================
                Chunk and Region check
            =====================================*/

            //playerChunk will be null when world first loads
            if (globalPlayerChunk == null)
                globalPlayerChunk = new Chunk().Initialize(player.GetChunkWithPlayer().GetLocation().X + RegionManager.CHUNK_BOUNDS,
                     player.GetChunkWithPlayer().GetLocation().Z);

            //playerRegion will be null when world first loads
            if (globalPlayerRegion == null)
            {
                globalPlayerRegion = player?.GetRegionWithPlayer();
                RegionManager.EnterRegion(globalPlayerRegion);
            }

            //Updates the chunks to render when the player has moved into a new chunk
            List<Chunk> chunksToRender = ChunkCache.GetChunksToRender();
            if (!player.GetChunkWithPlayer().Equals(globalPlayerChunk))
            {

                globalPlayerChunk = player.GetChunkWithPlayer();
                ChunkCache.SetPlayerChunk(globalPlayerChunk);
                ChunkCache.ReRender(true);

                chunksToRender = ChunkCache.GetChunksToRender();

                //Updates the regions when player moves into different region
                if (!player.GetRegionWithPlayer().Equals(globalPlayerRegion))
                {
                    globalPlayerRegion = player.GetRegionWithPlayer();
                    loadedWorld.UpdateVisibleRegions();
                }
            }
            //=========================================================================

            //Per chunk primitive information calculated in thread pool and later sent to GPU for drawing
            ConcurrentBag<RenderTask> renderTasks = [];
            CountdownEvent countdown = new(chunksToRender.Count);
            foreach (Chunk c in chunksToRender)
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(delegate (object state)
                {
                    renderTasks.Add(c.GetRenderTask());
                    countdown.Signal();
                }));
            }
           countdown.Wait();
           
            /*======================================================
            Getting vertex and element information for rendering
            ========================================================*/

            /*======================================================
            Getting vertex and element information for rendering
            ========================================================*/

            foreach (RenderTask renderTask in renderTasks)
            {
                /*=====================================
                 Vertex attribute definitions for shaders
                 ======================================*/

                if (renderTask == null)
                    continue;

                int posSize = 3;
                int layerSize = 1;
                int coordSize = 1;
                int floatSizeBytes = 4;
                int vertexSizeBytes = (posSize + layerSize + coordSize) * floatSizeBytes;

                // Position
                GL.VertexAttribPointer(0, posSize, VertexAttribPointerType.Float, false, vertexSizeBytes, 0);
                GL.EnableVertexAttribArray(0);

                // Texture Layer
                GL.VertexAttribPointer(1, layerSize, VertexAttribPointerType.Float, false, vertexSizeBytes, posSize * sizeof(float));
                GL.EnableVertexAttribArray(1);

                // Texture Coordinates
                GL.VertexAttribPointer(2, coordSize, VertexAttribPointerType.Float, false, vertexSizeBytes, (posSize + layerSize) * sizeof(float));
                GL.EnableVertexAttribArray(2);

                int vbo = renderTask.GetVbo();
                int ebo = renderTask.GetEbo();
                int vao = renderTask.GetVao();
                GL.BindVertexArray(vao);


                //Gets chunk data from previously submitted Future
                float[] vertices = renderTask.GetVertexData();
                int[] elements = renderTask.GetElementData();

                //Sends chunk data to GPU for drawing
                if (vertices.Length > 0 && elements.Length > 0)
                {
                    /*==================================
                    Buffer binding and loading
                    ====================================*/

                    //Vertices
                    float[] vertexBuffer = vertices;

                    // Create VBO upload the vertex buffer
                    GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
                    GL.BufferData(BufferTarget.ArrayBuffer, vertexBuffer.Length * sizeof(float), vertexBuffer, BufferUsageHint.StaticDraw);

                    //Elements
                    int[] elementBuffer = elements;

                    // Create EBO upload the element buffer
                    GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
                    GL.BufferData(BufferTarget.ElementArrayBuffer, elementBuffer.Length * sizeof(int), elementBuffer, BufferUsageHint.StaticDraw);

                    /*==================================
                    Drawing
                    ====================================*/

                    GL.DrawElements(PrimitiveType.TriangleStrip, elementBuffer.Length, DrawElementsType.UnsignedInt, 0);
                }
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            shaders?.Cleanup();
            TextureLoader.Unbind();
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);

            // Update the opengl viewport
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
            float FAR = 3000.0f;
            float NEAR = 0.01f;

            Matrix4 pMatrix = Matrix4.CreatePerspectiveFieldOfView(FOV, (float)e.Width / e.Height, NEAR, FAR);

            shaders?.SetMatrixUniform("projectionMatrix", pMatrix);

            // Tell ImGui of the new size
            _controller.WindowResized(ClientSize.X, ClientSize.Y);
        }

        public static Player GetPlayer() {
            if (player == null)
                player = new();

            return player;
        }
        public static RegionManager GetWorld()
        {
            return loadedWorld;
        }
        public static ShaderProgram GetShaders()
        {
            return shaders;
        }
        public static ConcurrentQueue<Action> GetMainThreadQueue()
        {
            return mainThreadQueue;
        }
        static void Main()
        {

            Window wnd = new(GameWindowSettings.Default, new NativeWindowSettings() {
                Location = new Vector2i(0, 0),
                ClientSize = new Vector2i(screenWidth, screenHeight),
                APIVersion = new Version(4, 1) });

            wnd.Run();
        }
    }
}
