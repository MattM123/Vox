using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using ImGuiNET;
using Newtonsoft.Json.Linq;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using Vox.GUI;
using Vox.Model;
using Vox.Texturing;
using Vox.World;
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
        private static ShaderProgram? shaderProgram = null;
        private static Player? player = null;
        private readonly float FOV = MathHelper.DegreesToRadians(50.0f);
        private RegionManager? loadedWorld;
        private static float angle = 0.0f;
        private static Chunk? globalPlayerChunk = null;
        private static Region? globalPlayerRegion = null;
        private static Matrix4 modelMatricRotate;
        private static Matrix4 viewMatrix;
        private static float fps = 0.0f;
        public static string assets = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\.voxelGame\\Assets\\";
        private static BlockModel menuModel;
        private static Chunk c; 


        protected override void OnLoad()
        {
            base.OnLoad();

            c = new Chunk().Initialize(0, 0, 0);

            //Load textures and models
            TextureLoader.LoadTextures();
            ModelLoader.LoadModels();
            c.GetRenderTask();

            //Block that renders in the world selection screen after shader setup
            menuModel = ModelLoader.GetModel(BlockType.STONE_BLOCK);

            Title += ": OpenGL Version: " + GL.GetString(StringName.Version);

            _controller = new ImGuiController(ClientSize.X, ClientSize.Y);
            shaderProgram = new();

            Directory.CreateDirectory(appFolder + "worlds");
            GL.Enable(EnableCap.DepthTest);

            //Enable primitive restart
            GL.Enable(EnableCap.PrimitiveRestart);
            GL.PrimitiveRestartIndex(10000);

            //========================
            //Shader Compilation
            //========================

            //Load the vertex shader from file
            string vertexShaderSource = ShaderProgram.LoadShaderFromFile("..\\..\\..\\Texturing\\VertexShader.glsl");
            shaderProgram.CreateVertexShader(vertexShaderSource);

            // Load the fragment shader from file
            string fragmentShaderSource = ShaderProgram.LoadShaderFromFile("..\\..\\..\\Texturing\\FragShader.glsl");
            shaderProgram.CreateFragmentShader(fragmentShaderSource);

            //Load Textures
            shaderProgram.UploadTexture("texture_sampler", 0);


            // Link the shader program
            shaderProgram.Link();

            // Use the shader program for rendering
            shaderProgram.Bind();

            //========================
            //Matrix setup
            //========================

            float FAR = 500.0f;
            float NEAR = 0.01f;
            Matrix4 pMatrix = Matrix4.CreatePerspectiveFieldOfView(FOV, (float) ClientSize.X / ClientSize.Y, NEAR, FAR);

            try
            {
                shaderProgram.CreateUniform("projectionMatrix");
                shaderProgram.SetUniform("projectionMatrix", pMatrix);
            }
            catch (Exception e1)
            {
                Logger.Error(e1, "Window.OnLoad");
            }
            
            try
            {
                shaderProgram.CreateUniform("modelMatrix");
                shaderProgram.SetUniform("modelMatrix", modelMatricRotate);
                shaderProgram.SetUniform("modelMatrix", modelMatricRotate);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Window.OnLoad");
            }

            viewMatrix = Matrix4.LookAt(new Vector3(50f, 180, -10f), new Vector3(8f, 150f, 8f), new Vector3(0.0f, 1f, 0.0f));
            try
            {
                shaderProgram.CreateUniform("viewMatrix");
                shaderProgram.SetUniform("viewMatrix", viewMatrix);
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }

        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);
            
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
              RenderMenu();
            }
            else
            {
                Logger.Debug("World Load");
                //glfwSetInputMode(windowPtr, GLFW_CURSOR, GLFW_CURSOR_DISABLED);
                renderMenu = false;
                //RenderWorld();
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
                modelMatricRotate = Matrix4.CreateFromAxisAngle(new Vector3(150f, 2200, 100f), angle);
                Matrix4 menuMatrix = Matrix4.CreateScale(1) * modelMatricRotate;
                shaderProgram?.SetUniform("modelMatrix", menuMatrix);


            }

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

            //   ImGui.ShowDemoWindow();
            //   ImGui.ShowStyleEditor();

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
                                player = new Player();
                                renderMenu = false;
                                RegionManager rm = new(folder);
                                loadedWorld = rm;
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

                //ImGui.Text("Region: " + Window.GetPlayer().GetRegionWithPlayer());
                ImGui.Text(GetPlayer().GetChunkWithPlayer().ToString());
                ImGui.Text("Chunk Cache Size: " + ChunkCache.GetChunksToRender().Count);
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

                ImGui.Text(viewMatrix.ToString());
                ImGui.End();
            }
        }

        public static bool IsMenuRendered() { return renderMenu; }

        private static void RenderMenu()
        {
            int vboID, vaoID, eboID;     
            List<float> vertices = [];

            vertices.AddRange(ModelUtils.GetCuboidFace(menuModel, "south"));
            vertices.AddRange(ModelUtils.GetCuboidFace(menuModel, "north"));
            vertices.AddRange(ModelUtils.GetCuboidFace(menuModel, "up"));
            vertices.AddRange(ModelUtils.GetCuboidFace(menuModel, "down"));
            vertices.AddRange(ModelUtils.GetCuboidFace(menuModel, "west"));
            vertices.AddRange(ModelUtils.GetCuboidFace(menuModel, "east"));

            // Declares the Elements Array, where the indices to be drawn are stored
            int[] elementArray = {
                //Front face
                0, 1, 2, 3, 80000,
                //Back face
                4, 5, 6, 7, 80000,
                //Left face
                8, 9, 10, 11, 80000,
                //Right face
                12, 13, 14, 15, 80000,
                //Top face
                16, 17, 18, 19, 80000,
                //Bottom face
                20, 21, 22, 23, 80000
            };

            /*==================================
            Buffer binding and loading
            ====================================*/


            vboID = GL.GenBuffer();
            eboID = GL.GenBuffer();
            vaoID = GL.GenVertexArray();

            GL.BindVertexArray(vaoID);

            //Vertices
            //float[] vertexBuffer = [.. vertices];
            float[] vertexBuffer = c.GetVertices();

            // Create VBO upload the vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboID);
            GL.BufferData(BufferTarget.ArrayBuffer, vertexBuffer.Length * sizeof(float), vertexBuffer, BufferUsageHint.StaticDraw);

            //Elements
            //int[] elementBuffer = elementArray;
            int[] elementBuffer = c.GetElements();


            // Create EBO upload the element buffer;
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, eboID);
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
            GL.DrawElements(PrimitiveType.TriangleStrip, c.GetElements().Length, DrawElementsType.UnsignedInt, 0);

            //Unbind and cleanup everything
            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
            GL.DisableVertexAttribArray(2);

            // Delete VAO, VBO, and EBO
            GL.DeleteVertexArray(vaoID);
            GL.DeleteBuffer(vboID);
            GL.DeleteBuffer(eboID);
        }

        /*
        private void RenderWorld()
        {
            int vboID, eboID, vaoID;

              if (player.GetPosition().Y < 100)
                   player.SetPosition(new Vector3(player.GetPosition().X, 300, player.GetPosition().Z));

            /*=====================================
             View Matrix setup
            ======================================*/
        /*     try
             {
                 shaderProgram.CreateUniform("viewMatrix");
             }
             catch (Exception e)
             {
                 Logger.Error(e);
             }
             shaderProgram.SetUniform("viewMatrix", player.GetViewMatrix());


             try
             {
                 shaderProgram.CreateUniform("modelMatrix");
             }
             catch (Exception e)
             {
                 Logger.Error(e);
             }
             shaderProgram.SetUniform("modelMatrix", Chunk.GetModelMatrix());

             /*====================================
                 Chunk and Region check
             =====================================*/
        //playerChunk will be null when world first loads
        /*     if (globalPlayerChunk == null)
                 globalPlayerChunk = player.GetChunkWithPlayer();

             //playerRegion will be null when world first loads
             if (globalPlayerRegion == null)
             {
                 globalPlayerRegion = player.GetRegionWithPlayer();
                 RegionManager.EnterRegion(globalPlayerRegion);
             }

             //Updates the chunks to render when the player has moved into a new chunk
             List<Chunk> chunksToRender = ChunkCache.GetChunksToRender();
             if (!player.GetChunkWithPlayer().GetLocation().Equals(globalPlayerChunk.GetLocation()))
             {
                 globalPlayerChunk = player.GetChunkWithPlayer();
                 ChunkCache.SetPlayerChunk(globalPlayerChunk);
                 chunksToRender = ChunkCache.GetChunksToRender();

                 //Updates the regions when player moves into different region
                 if (!player.GetRegionWithPlayer().Equals(globalPlayerRegion))
                 {
                     globalPlayerRegion = player.GetRegionWithPlayer();
                     RegionManager.UpdateVisibleRegions();
                 }
             }
             vboID = GL.GenBuffer();
             eboID = GL.GenBuffer();

             vaoID = GL.GenVertexArray();
             GL.BindVertexArray(vaoID);

                /*=====================================
                 Vertex attribute definitions for shaders
                 ======================================*/
        /*     int posSize = 3;
             int colorSize = 4;
             int uvSize = 2;
             int floatSizeBytes = 4;
             int vertexSizeBytes = (posSize + colorSize + uvSize) * floatSizeBytes;

             //Position
             glVertexAttribPointer(0, posSize, GL_FLOAT, false, vertexSizeBytes, 0);
             glEnableVertexAttribArray(0);

             //Color
             glVertexAttribPointer(1, colorSize, GL_FLOAT, false, vertexSizeBytes, posSize * Float.BYTES);
             glEnableVertexAttribArray(1);

             //Texture
             glVertexAttribPointer(2, uvSize, GL_FLOAT, false, vertexSizeBytes, (posSize + colorSize) * Float.BYTES);
             glEnableVertexAttribArray(2);

             //Per chunk primitive information calculated in thread pool and later sent to GPU for drawing
             ArrayList<Future<RenderTask>> renderTasks = new ArrayList<>();


             //TESTSETSETSETSETSETSETS
             /*==================================
                    Buffer binding and loading
               ====================================*/
        /*       TextureCoordinateStore grassFront = BlockType.GRASS.GetFrontCoords();
               TextureCoordinateStore grassBack = BlockType.GRASS.GetBackCoords();
               TextureCoordinateStore grassLeft = BlockType.GRASS.GetLeftCoords();
               TextureCoordinateStore grassRight = BlockType.GRASS.GetRightCoords();
               TextureCoordinateStore grassTop = BlockType.GRASS.GetTopCoords();
               TextureCoordinateStore grassBott = BlockType.GRASS.GetBottomCoords();


               float[] vertices1 = {
                   //Position (X, Y, Z)    Color (R, G, B, A)          Texture (U, V)
                   // Front face
                   -0.5f, -0.5f, 0.5f,     1.0f, 0.0f, 0.0f, 0.0f,     grassFront.GetBottomLeft()[0], grassFront.GetBottomLeft()[1],
                   0.5f, -0.5f,  0.5f,     1.0f, 0.0f, 0.0f, 0.0f,     grassFront.GetBottomRight()[0], grassFront.GetBottomRight()[1],
                   -0.5f, 0.5f,  0.5f,     1.0f, 0.0f, 0.0f, 0.0f,     grassFront.GetTopLeft()[0], grassFront.GetTopLeft()[1],
                   0.5f,  0.5f,  0.5f,     1.0f, 0.0f, 0.0f, 0.0f,     grassFront.GetTopRight()[0], grassFront.GetTopRight()[1],

                   // Back face
                   -0.5f, -0.5f, -0.5f,    0.0f, 1.0f, 0.0f, 0.0f,     grassBack.GetBottomLeft()[0], grassBack.GetBottomLeft()[1],
                   0.5f, -0.5f, -0.5f,     0.0f, 1.0f, 0.0f, 0.0f,     grassBack.GetBottomRight()[0], grassBack.GetBottomRight()[1],
                   -0.5f,  0.5f, -0.5f,    0.0f, 1.0f, 0.0f, 0.0f,     grassBack.GetTopLeft()[0], grassBack.GetTopLeft()[1],
                   0.5f,  0.5f, -0.5f,     0.0f, 1.0f, 0.0f, 0.0f,     grassBack.GetTopRight()[0], grassBack.GetTopRight()[1],

                   // Left face
                   -0.5f, -0.5f, -0.5f,    0.0f, 0.0f, 1.0f, 0.0f,     grassLeft.GetBottomLeft()[0], grassLeft.GetBottomLeft()[1],
                   -0.5f,  0.5f, -0.5f,    0.0f, 0.0f, 1.0f, 0.0f,     grassLeft.GetTopLeft()[0], grassLeft.GetTopLeft()[1],
                   -0.5f, -0.5f, 0.5f,     0.0f, 0.0f, 1.0f, 0.0f,     grassLeft.GetBottomRight()[0], grassLeft.GetBottomRight()[1],
                   -0.5f,  0.5f, 0.5f,     0.0f, 0.0f, 1.0f, 0.0f,     grassLeft.GetTopRight()[0], grassLeft.GetTopRight()[1],

                   // Right face
                   0.5f, -0.5f, -0.5f,     1.0f, 1.0f, 0.0f, 0.0f,     grassRight.GetBottomRight()[0], grassRight.GetBottomRight()[1],
                   0.5f,  0.5f, -0.5f,     1.0f, 1.0f, 0.0f, 0.0f,     grassRight.GetTopRight()[0], grassRight.GetTopRight()[1],
                   0.5f, -0.5f, 0.5f,      1.0f, 1.0f, 0.0f, 0.0f,     grassRight.GetBottomLeft()[0], grassRight.GetBottomLeft()[1],
                   0.5f,  0.5f, 0.5f,      1.0f, 1.0f, 0.0f, 0.0f,     grassRight.GetTopLeft()[0], grassRight.GetTopLeft()[1],

                   // Top face
                   -0.5f, 0.5f, -0.5f,     0.0f, 1.0f, 1.0f, 0.0f,     grassTop.GetBottomLeft()[0], grassTop.GetBottomLeft()[1],
                   0.5f,  0.5f, -0.5f,     0.0f, 1.0f, 1.0f, 0.0f,     grassTop.GetBottomRight()[0], grassTop.GetBottomRight()[1],
                   -0.5f, 0.5f,  0.5f,     0.0f, 1.0f, 1.0f, 0.0f,     grassTop.GetTopLeft()[0], grassTop.GetTopLeft()[1],
                   0.5f,  0.5f,  0.5f,     0.0f, 1.0f, 1.0f, 0.0f,     grassTop.GetTopRight()[0], grassTop.GetTopRight()[1],

                   // Bottom face
                   -0.5f, -0.5f, -0.5f,    1.0f, 0.0f, 1.0f, 0.0f,     grassBott.GetBottomLeft()[0], grassBott.GetBottomLeft()[1],
                   0.5f,  -0.5f, -0.5f,    1.0f, 0.0f, 1.0f, 0.0f,     grassBott.GetBottomRight()[0], grassBott.GetBottomRight()[1],
                   -0.5f, -0.5f,  0.5f,    1.0f, 0.0f, 1.0f, 0.0f,     grassBott.GetTopLeft()[0], grassBott.GetTopLeft()[1],
                   0.5f,  -0.5f,  0.5f,    1.0f, 0.0f, 1.0f, 0.0f,     grassBott.GetTopRight()[0], grassBott.GetTopRight()[1],
           };

               // Declares the Elements Array, where the indices to be drawn are stored
               int[] elements1 = {
                   //Front face
                   0, 1, 2, 3, 80000,
                   //Back face
                   4, 5, 6, 7, 80000,
                   //Left face
                   8, 9, 10, 11, 80000,
                   //Right face
                   12, 13, 14, 15, 80000,
                   //Top face
                   16, 17, 18, 19, 80000,
                   //Bottom face
                   20, 21, 22, 23, 80000
           };
               //     vboID = glGenBuffers();
               //     eboID = glGenBuffers();
               //    vaoID = glGenVertexArrays();

               //   glBindVertexArray(vaoID);

               //Vertices
               FloatBuffer vertexBuffer1 = MemoryUtil.memAllocFloat(vertices1.length);
               vertexBuffer1.put(vertices1).flip();

               // Create VBO upload the vertex buffer
               glBindBuffer(GL_ARRAY_BUFFER, vboID);
               glBufferData(GL_ARRAY_BUFFER, vertexBuffer1, GL_STATIC_DRAW);

               //Elements
               IntBuffer elementBuffer1 = MemoryUtil.memAllocInt(elements1.length);
               elementBuffer1.put(elements1).flip();

               // Create EBO upload the element buffer
               glBindBuffer(GL_ELEMENT_ARRAY_BUFFER, eboID);
               glBufferData(GL_ELEMENT_ARRAY_BUFFER, elementBuffer1, GL_STATIC_DRAW);

               /*==================================
               Drawing
               ====================================*/
        /*     glDrawElements(GL_TRIANGLE_STRIP, elements1.length, GL_UNSIGNED_INT, 0);

             MemoryUtil.memFree(elementBuffer1);
             MemoryUtil.memFree(vertexBuffer1);



             //Gets ChunkCache mesh data in a non-blocking manner
             for (Chunk c : chunksToRender)
             {
                 c.SetEbo(glGenBuffers());
                 c.SetVbo(glGenBuffers());
                 renderTasks.add(Main.executor.submit(c::getRenderTask));
             }

             /*======================================================
              Getting vertex and element information for rendering
             ========================================================*/
        /*     for (Future<RenderTask> chunkRenderTask : renderTasks)
             {

                 float[] vertices = new float[0];
                 int[] elements = new int[0];
                 RenderTask task = null;

                 //Gets chunk data from previously submitted Future
                 try
                 {
                     task = chunkRenderTask.Get();
                     vertices = task.GetVertexData();
                     elements = task.GetElementData();
                 }
                 catch (InterruptedException | ExecutionException e) {
                Logger.Error(e);
             }



             //Sends chunk data to GPU for drawing
             if (vertices.length > 0 && elements.length > 0)
             {
                 /*==================================
                 Buffer binding and loading
                 ====================================*/

        //Vertices
        /*          FloatBuffer vertexBuffer = MemoryUtil.memAllocFloat(vertices.length);
                  vertexBuffer.put(vertices).flip();

                  // Create VBO upload the vertex buffer
                  glBindBuffer(GL_ARRAY_BUFFER, task.GetVbo());
                  glBufferData(GL_ARRAY_BUFFER, vertexBuffer, GL_STATIC_DRAW);

                  //Elements
                  IntBuffer elementBuffer = MemoryUtil.memAllocInt(elements.length);
                  elementBuffer.put(elements).flip();

                  // Create EBO upload the element buffer
                  glBindBuffer(GL_ELEMENT_ARRAY_BUFFER, task.GetEbo());
                  glBufferData(GL_ELEMENT_ARRAY_BUFFER, elementBuffer, GL_STATIC_DRAW);

                  /*==================================
                  Drawing
                  ====================================*/
        /*         glDrawElements(GL_TRIANGLE_STRIP, elements.length, GL_UNSIGNED_INT, 0);

                 MemoryUtil.memFree(elementBuffer);
                 MemoryUtil.memFree(vertexBuffer);
             }
             else
             {
                 logger.warning("Chunk has no data or inconsistent data");
             }
         }

         //Unbind and cleanup everything
         renderTasks.Clear();
         glDisableVertexAttribArray(0);
         glDisableVertexAttribArray(1);
         glDisableVertexAttribArray(2);
         glBindVertexArray(0);
         cleanupBuffers();
     }
     */

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            shaderProgram?.Cleanup();
            TextureLoader.Unbind();
            ModelLoader.Destroy();
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);

            // Update the opengl viewport
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
            float FAR = 3000.0f;
            float NEAR = 0.01f;

            Matrix4 pMatrix = Matrix4.CreatePerspectiveFieldOfView(FOV, (float) e.Width / e.Height, NEAR, FAR);

            shaderProgram?.SetUniform("projectionMatrix", pMatrix);

            // Tell ImGui of the new size
            _controller.WindowResized(ClientSize.X, ClientSize.Y);
        }

        public static Player GetPlayer() {
            if (player == null)
                player = new();

            return player; 
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
