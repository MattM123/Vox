using System.Buffers;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using ImGuiNET;
using OpenTK.Compute.OpenCL;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Vox.Genesis;
using Vox.GUI;
using Vox.Model;
using Vox.Rendering;
using BlendingFactor = OpenTK.Graphics.OpenGL4.BlendingFactor;
using BufferTarget = OpenTK.Graphics.OpenGL4.BufferTarget;
using BufferUsageHint = OpenTK.Graphics.OpenGL4.BufferUsageHint;
using ClearBufferMask = OpenTK.Graphics.OpenGL4.ClearBufferMask;
using DrawBufferMode = OpenTK.Graphics.OpenGL4.DrawBufferMode;
using DrawElementsType = OpenTK.Graphics.OpenGL4.DrawElementsType;
using EnableCap = OpenTK.Graphics.OpenGL4.EnableCap;
using FramebufferAttachment = OpenTK.Graphics.OpenGL4.FramebufferAttachment;
using FramebufferTarget = OpenTK.Graphics.OpenGL4.FramebufferTarget;
using GL = OpenTK.Graphics.OpenGL4.GL;
using HintMode = OpenTK.Graphics.OpenGL4.HintMode;
using HintTarget = OpenTK.Graphics.OpenGL4.HintTarget;
using PixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat;
using PixelType = OpenTK.Graphics.OpenGL4.PixelType;
using PrimitiveType = OpenTK.Graphics.OpenGL4.PrimitiveType;
using ReadBufferMode = OpenTK.Graphics.OpenGL4.ReadBufferMode;
using StringName = OpenTK.Graphics.OpenGL4.StringName;
using TextureMagFilter = OpenTK.Graphics.OpenGL4.TextureMagFilter;
using TextureMinFilter = OpenTK.Graphics.OpenGL4.TextureMinFilter;
using TextureParameterName = OpenTK.Graphics.OpenGL4.TextureParameterName;
using TextureTarget = OpenTK.Graphics.OpenGL4.TextureTarget;
using TextureWrapMode = OpenTK.Graphics.OpenGL4.TextureWrapMode;
using Vector3 = OpenTK.Mathematics.Vector3;
using VertexAttribPointerType = OpenTK.Graphics.OpenGL4.VertexAttribPointerType;


namespace Vox
{
    public class Window(GameWindowSettings windowSettings, NativeWindowSettings nativeSettings) : GameWindow(windowSettings, nativeSettings)
    {
        ImGuiController _UIController;
        public static readonly int screenWidth = Monitors.GetPrimaryMonitor().ClientArea.Size.X;
        public static readonly int screenHeight = Monitors.GetPrimaryMonitor().ClientArea.Size.Y - 100;

        private static bool renderMenu = true;
        private static readonly string appFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\.voxelGame\\";
        private static ShaderProgram? terrainShaders = null;
        private static ShaderProgram? lightingShaders = null;
        private static ShaderProgram? crosshairShader = null;
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
        private static Chunk menuChunk;
        private static long menuSeed;
        private float mouseSensitivity = 0.1f;
        private static int vbo, ebo, vao = 0;
        private static int lightVao, lightEbo, lightVbo;
        private static Vector3 _lightPos = new(0.0f, RegionManager.CHUNK_HEIGHT + 100, 0.0f);
        private static int crosshairTex;
        private static int sunlightDepthMapFBO;
        private static int sunlightDepthMap;
        private static Matrix4 pMatrix;
        private Matrix4 sunlightProjectionMatrix;
        private Matrix4 sunlightViewMatrix;
        private static Matrix4 lightSpaceMatrix;
        public static int primRestart = 500000;

        //used for player and lighting projection matrices
        private static float FAR = 500.0f;
        private static float NEAR = 0.01f;

        private static Vector3 lightColor;
        private static Vector3 ambientColor;
        private static Vector3 diffuseColor;

        protected override void OnLoad()
        {
            base.OnLoad();

            int texArray = TextureLoader.LoadTextures(0);
            terrainShaders = new();
            crosshairShader = new();
            lightingShaders = new();

            //Generate menu chunk seed
            byte[] buffer = new byte[8];
            RandomNumberGenerator.Fill(buffer); // Fills the buffer with random bytes
            menuSeed = BitConverter.ToInt64(buffer, 0);


            //Mene render buffers
            vbo = GL.GenBuffer();
            ebo = GL.GenBuffer();
            vao = GL.GenVertexArray();

            lightVbo = GL.GenBuffer();
            lightVao = GL.GenVertexArray();
            lightEbo = GL.GenBuffer();

            //Load textures and models

            //crosshairTex = TextureLoader.LoadSingleTexture(Path.Combine(assets, "Textures", "Crosshair_06.png"));

            ModelLoader.LoadModels();
            menuChunk = new Chunk().Initialize(0, 0, 0);
            menuChunk.GenerateRenderData();

            Title += ": OpenGL Version: " + GL.GetString(StringName.Version);

            _UIController = new ImGuiController(ClientSize.X, ClientSize.Y);

            Directory.CreateDirectory(appFolder + "worlds");
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.LineSmooth);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Enable(EnableCap.Blend);
            GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);

            //Enable primitive restart
            GL.Enable(EnableCap.PrimitiveRestart);
            GL.PrimitiveRestartIndex(primRestart);

            //========================
            //Shader Compilation
            //========================

            //-----------------------Terrain shaders---------------------------------
            //Load main vertex shader from file
            string vertexTerrainShaderSource = ShaderProgram.LoadShaderFromFile("..\\..\\..\\Rendering\\VertexTerrainShader.glsl");
            terrainShaders.CreateVertexShader(vertexTerrainShaderSource);

            // Load main fragment shader from file
            string fragmentTerrainShaderSource = ShaderProgram.LoadShaderFromFile("..\\..\\..\\Rendering\\FragTerrainShader.glsl");
            terrainShaders.CreateFragmentShader(fragmentTerrainShaderSource);
            //-----------------------Terrain shaders---------------------------------

            //-----------------------Lihgting shaders---------------------------------
            //Load main vertex shader from file
            string vertexLightingShaderSource = ShaderProgram.LoadShaderFromFile("..\\..\\..\\Rendering\\VertexLightingShader.glsl");
            lightingShaders.CreateVertexShader(vertexLightingShaderSource);

            // Load main fragment shader from file
            string fragmentLightingSource = ShaderProgram.LoadShaderFromFile("..\\..\\..\\Rendering\\FragLightingShader.glsl");
            lightingShaders.CreateFragmentShader(fragmentLightingSource);
            //-----------------------Lihgting shaders---------------------------------

            //Load crosshair vertex shaders
            string vertexCrosshairSource = ShaderProgram.LoadShaderFromFile("..\\..\\..\\Rendering\\Vertex_Crosshair.glsl");
            crosshairShader.CreateVertexShader(vertexCrosshairSource);

            //Load crosshair frag shaders
            string fragmentCrosshairSource = ShaderProgram.LoadShaderFromFile("..\\..\\..\\Rendering\\Frag_Crosshair.glsl");
            crosshairShader.CreateFragmentShader(fragmentCrosshairSource);

            terrainShaders.Link();
            lightingShaders.Link();

            //Sunlight frame buffer for shadow map
            sunlightDepthMapFBO = GL.GenFramebuffer();
            sunlightDepthMap = GL.GenTexture();

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, sunlightDepthMap);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent,
                 ClientSize.X, ClientSize.Y, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareFunc, (int)DepthFunction.Lequal);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareMode, (int)TextureCompareMode.CompareRefToTexture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

            //Attach depth texture to frame buffer         
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, sunlightDepthMapFBO);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, sunlightDepthMap, 0);
            GL.DrawBuffer(DrawBufferMode.None);
            GL.ReadBuffer(ReadBufferMode.None);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);



            //========================
            //Matrix unifrom setup
            //========================

            pMatrix = Matrix4.CreatePerspectiveFieldOfView(FOV, (float)ClientSize.X / ClientSize.Y, NEAR, FAR);

            viewMatrix = Matrix4.LookAt(new Vector3(-10f, 220f, -20f), new Vector3(8f, 200f, 8f), new Vector3(0.0f, 1f, 0.0f));
            sunlightViewMatrix = Matrix4.LookAt(_lightPos, new(0, 0, 0), Vector3.UnitY);

            sunlightProjectionMatrix = Matrix4.CreateOrthographicOffCenter(-10.0f, 10.0f, -10.0f, 10.0f, NEAR, 800);

            lightingShaders.Bind();

            lightingShaders.CreateUniform("lightModel");
            lightingShaders?.CreateUniform("lightViewMatrix");
            lightingShaders?.CreateUniform("lightProjMatrix");

            terrainShaders.Bind();

            //Load Textures
            terrainShaders.CreateUniform("texture_sampler");
            terrainShaders.CreateUniform("sunlightDepth_sampler");

            terrainShaders.UploadAndBindTexture("texture_sampler", 0, texArray, (OpenTK.Graphics.OpenGL.TextureTarget)TextureTarget.Texture2DArray);
            terrainShaders.UploadAndBindTexture("sunlightDepth_sampler", 1, sunlightDepthMap, (OpenTK.Graphics.OpenGL.TextureTarget)TextureTarget.Texture2D);
            //------------

            terrainShaders.CreateUniform("chunkSize");
            terrainShaders.SetIntFloatUniform("chunkSize", RegionManager.CHUNK_BOUNDS);

            terrainShaders.CreateUniform("crosshairOrtho");
            terrainShaders.SetMatrixUniform("crosshairOrtho", Matrix4.CreateOrthographic(screenWidth, screenHeight, 0.1f, 10f));

            terrainShaders.CreateUniform("lightSpaceMatrix");
            terrainShaders.CreateUniform("playerMin");
            terrainShaders.CreateUniform("playerMax");
            terrainShaders.CreateUniform("blockCenter");
            terrainShaders.CreateUniform("curPos");
            terrainShaders.CreateUniform("localHit");
            terrainShaders.CreateUniform("renderDistance");
            terrainShaders.CreateUniform("playerPos");
            terrainShaders.CreateUniform("forwardDir");
            terrainShaders.CreateUniform("targetVertex");
            terrainShaders.CreateUniform("projectionMatrix");
            terrainShaders.CreateUniform("modelMatrix");
            terrainShaders.CreateUniform("viewMatrix");
            terrainShaders.CreateUniform("chunkModelMatrix");
            terrainShaders.CreateUniform("isMenuRendered");

            // lightingShaders.CreateUniform("chunkModelMatrix");
            // lightingShaders.CreateUniform("projectionMatrix");
            // lightingShaders.CreateUniform("modelMatrix");
            // lightingShaders.CreateUniform("viewMatrix");

            // This is where we change the lights color over time using the sin function
            float time = DateTime.Now.Second + DateTime.Now.Millisecond / 1000f;
            lightColor.X = 1.4f; //(MathF.Sin(time * 2.0f) + 1) / 2f;
            lightColor.Y = 1f;//(MathF.Sin(time * 0.7f) + 1) / 2f;
            lightColor.Z = 1f; //(MathF.Sin(time * 1.3f) + 1) / 2f;
            //lightColor.X = (MathF.Sin(time * 2.0f) + 1) / 2f;
            //lightColor.Y = (MathF.Sin(time * 0.7f) + 1) / 2f;
            //lightColor.Z = (MathF.Sin(time * 1.3f) + 1) / 2f;

            // The ambient light is less intensive than the diffuse light in order to make it less dominant
            ambientColor = lightColor * new Vector3(0.2f);
            diffuseColor = lightColor * new Vector3(0.5f);

            sunlightProjectionMatrix = Matrix4.CreateOrthographicOffCenter(-10.0f, 10.0f, -10.0f, 10.0f, NEAR, FAR);


        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            // GL.ActiveTexture(TextureUnit.Texture1);
            //
            // //Attach depth texture to frame buffer         
            // GL.BindFramebuffer(FramebufferTarget.Framebuffer, sunlightDepthMapFBO);
            // GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, sunlightDepthMap, 0);
            // GL.DrawBuffer(DrawBufferMode.None);
            // GL.ReadBuffer(ReadBufferMode.None);
            // GL.BindTexture(TextureTarget.Texture2D, 0);
            // GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            //
            //
            //
            //  //Bind FBO and clear depth
            //  GL.BindFramebuffer(FramebufferTarget.Framebuffer, sunlightDepthMapFBO);
            //  GL.ClearDepth(1.0f);
            //  GL.Clear(ClearBufferMask.DepthBufferBit);
            //
            //
            //  //Check FBO
            //  FramebufferErrorCode status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            //  if (status != FramebufferErrorCode.FramebufferComplete)
            //  {
            //      Console.WriteLine($"Framebuffer error: {status}");
            //  }
            //
            //  lightingShaders?.Bind();
            //  GL.BindVertexArray(lightVao);
            //
            //  // Create VBO upload the vertex buffer
            //  List<Vector3> buff = [];
            //  foreach (TerrainVertex v in menuChunk.GetVertices())
            //      buff.Add(v.GetVector());
            //
            //
            //  GL.BindBuffer(BufferTarget.ArrayBuffer, lightVbo);
            //  GL.BufferData(BufferTarget.ArrayBuffer, buff.Count * Unsafe.SizeOf<Vector3>(), buff.ToArray(), BufferUsageHint.StaticDraw);
            //
            //  int shadowPos = 3;
            //
            //  // Position
            //  GL.VertexAttribPointer(0, shadowPos, VertexAttribPointerType.Float, false, Unsafe.SizeOf<Vector3>(), 0);
            //  GL.EnableVertexAttribArray(0);


            // GL.DrawArrays(PrimitiveType.Points, 0, buff.Count);

            /*======================================
            World and Menu rendering - DEPTH PRE-PASS
            =======================================*/

            //  if (loadedWorld == null)
            //  {
            //      if (angle > 360)
            //          angle = 0.0f;
            //
            //      angle += 0.0002f;
            //      renderMenu = true;
            //      terrainShaders?.SetIntFloatUniform("isMenuRendered", 1);
            //      RenderMenu();
            //  }
            //  else
            //  {
            //      renderMenu = false;
            //      terrainShaders?.SetIntFloatUniform("isMenuRendered", 0);
            //      RenderWorld();
            //  }

            //Unbind sunlight depth map at the end of frame render
            //  GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            //     terrainShaders.Bind();

            /*=====================================
            Vertex attribute definitions for shaders
            ======================================*/
            int posSize = 3;
            int layerSize = 1;
            int coordSize = 1;
            int lightSize = 1;
            int normalSize = 3;
            int faceSize = 1;
            int typeSize = 1;

            // Position
            GL.VertexAttribPointer(0, posSize, VertexAttribPointerType.Float, false, Unsafe.SizeOf<TerrainVertex>(), 0);
            GL.EnableVertexAttribArray(0);

            // Texture Layer
            GL.VertexAttribPointer(1, layerSize, VertexAttribPointerType.Float, false, Unsafe.SizeOf<TerrainVertex>(), (posSize) * sizeof(float));
            GL.EnableVertexAttribArray(1);

            // Texture Coordinates
            GL.VertexAttribPointer(2, coordSize, VertexAttribPointerType.Float, false, Unsafe.SizeOf<TerrainVertex>(), (posSize + layerSize) * sizeof(float));
            GL.EnableVertexAttribArray(2);

            // Light
            GL.VertexAttribPointer(3, lightSize, VertexAttribPointerType.Float, false, Unsafe.SizeOf<TerrainVertex>(), (posSize + layerSize + coordSize) * sizeof(float));
            GL.EnableVertexAttribArray(3);

            // Normals
            GL.VertexAttribPointer(4, normalSize, VertexAttribPointerType.Float, false, Unsafe.SizeOf<TerrainVertex>(), (posSize + layerSize + coordSize + lightSize) * sizeof(float));
            GL.EnableVertexAttribArray(4);

            // Block type
            GL.VertexAttribPointer(5, typeSize, VertexAttribPointerType.Float, false, Unsafe.SizeOf<TerrainVertex>(), (posSize + layerSize + coordSize + lightSize + faceSize + normalSize) * sizeof(float));
            GL.EnableVertexAttribArray(5);

            // Block face
            GL.VertexAttribPointer(6, faceSize, VertexAttribPointerType.Float, false, Unsafe.SizeOf<TerrainVertex>(), (posSize + layerSize + coordSize + lightSize + faceSize + normalSize + typeSize) * sizeof(float));
            GL.EnableVertexAttribArray(6);


            /*==============================
            Update UI input and config
            ===============================*/
            _UIController.Update(this, (float)e.Time);


            GL.ClearColor(0.5f, 0.8f, 1.0f, 0.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);

            //Update depth texture
          //  GL.ActiveTexture(TextureUnit.Texture1);
          //  GL.BindTexture(TextureTarget.Texture2D, sunlightDepthMap);
          //  GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent,
          //       ClientSize.X, ClientSize.Y, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
          //  GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);

            //Attach depth texture to frame buffer         
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, sunlightDepthMapFBO);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, sunlightDepthMap, 0);
            GL.DrawBuffer(DrawBufferMode.None);
            GL.ReadBuffer(ReadBufferMode.None);
            //GL.BindTexture(TextureTarget.Texture2D, 0);
            // GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);



            if (loadedWorld == null)
            {
                if (angle > 360)
                    angle = 0.0f;

                angle += 0.1f * (float) e.Time;
                renderMenu = true;
                terrainShaders?.Bind();
                terrainShaders?.SetIntFloatUniform("isMenuRendered", 1);
                RenderMenu();
            }
            else
            {
                renderMenu = false;
                terrainShaders?.Bind();
                terrainShaders?.SetIntFloatUniform("isMenuRendered", 0);
                RenderWorld();
           
            }

            ImGuiController.CheckGLError("End of frame");

            /*======================================
            Render new UI frame over everything
            ========================================*/
            RenderUI();
            _UIController.Render();

            SwapBuffers();

        }

        private static void SetTerrainShaderUniforms()
        {
         // lightingShaders?.Bind();
         // if (!IsMenuRendered())
         //     sunlightViewMatrix = Matrix4.LookAt(_lightPos, player.GetPosition(), Vector3.UnitY);
         // else
         //     sunlightViewMatrix = Matrix4.LookAt(_lightPos, new(0, 0, 0), Vector3.UnitY);
         // 
         // 
         // lightSpaceMatrix = sunlightProjectionMatrix * sunlightViewMatrix;
           

            terrainShaders?.Bind();
          //  terrainShaders?.SetMatrixUniform("lightSpaceMatrix", lightSpaceMatrix);
            terrainShaders?.SetMatrixUniform("projectionMatrix", pMatrix);
            terrainShaders?.SetMatrixUniform("modelMatrix", modelMatricRotate);

            if (IsMenuRendered())
            {
                modelMatricRotate = Matrix4.CreateFromAxisAngle(new Vector3(100f, 2000, 100f), angle);
                Matrix4 menuMatrix = modelMatricRotate;
                terrainShaders?.SetMatrixUniform("modelMatrix", menuMatrix);
                terrainShaders?.SetMatrixUniform("viewMatrix", viewMatrix);
            }
            else
            {
                terrainShaders?.SetVector3Uniform("playerMin", GetPlayer().GetBoundingBox()[0]);
                terrainShaders?.SetVector3Uniform("playerMax", GetPlayer().GetBoundingBox()[1]);
                terrainShaders?.SetVector3Uniform("forwardDir", GetPlayer().GetForwardDirection());
                terrainShaders?.SetMatrixUniform("viewMatrix", GetPlayer().GetViewMatrix());

            }

            terrainShaders?.SetVector3Uniform("playerPos", GetPlayer().GetPosition());

            if (loadedWorld != null)
                terrainShaders?.SetIntFloatUniform("renderDistance", RegionManager.GetRenderDistance());

            terrainShaders?.SetMatrixUniform("chunkModelMatrix", Chunk.GetModelMatrix());


            terrainShaders?.SetIntFloatUniform("isMenuRendered", 1);
            terrainShaders?.SetVector3Uniform("playerMin", GetPlayer().GetBoundingBox()[0]);
            terrainShaders?.SetVector3Uniform("playerMax", GetPlayer().GetBoundingBox()[1]);
            terrainShaders?.SetVector3Uniform("targetVertex", GetPlayer().UpdateViewTarget(out _, out _).GetLowerCorner());

            //material uniforms
            terrainShaders?.SetVector3Uniform("material.ambient", new Vector3(1.0f, 0.5f, 0.31f));
            terrainShaders?.SetVector3Uniform("material.diffuse", new Vector3(1.5f, 1.5f, 1.5f));
            terrainShaders?.SetVector3Uniform("material.specular", new Vector3(0.5f, 0.5f, 0.5f));
            terrainShaders?.SetIntFloatUniform("material.shininess", 32.0f);
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);


            //Update player and cursor state
            if (!IsMenuRendered())
            {
                GetPlayer().Update((float)args.Time);
                CursorState = CursorState.Grabbed;
                Player.SetLookDir(MouseState.Y, MouseState.X);
            }

            /*==================================
             Ambient lighting update
            ====================================*/
            //update light color each frame
            float dayLengthInSeconds = 60.0f;  
            float timeOfDay = (DateTime.Now.Second + DateTime.Now.Millisecond / 1000f) % dayLengthInSeconds;
            float normalizedTime = timeOfDay / dayLengthInSeconds;  // Normalized time between 0 (start of day) and 1 (end of day)
            float dayCycle = MathF.Sin(normalizedTime * MathF.PI * 2);  // Sine wave oscillates between -1 and 1 over one full cycle
            float lightIntensity = (((dayCycle + 1.0f) / 2f) + 0.1f) * 2;  // Convert sine range (-1 to 1) to (0 to 1)

            lightColor.X = lightIntensity * 1.0f;//R
            lightColor.Y = lightIntensity * 0.7f;//G
            lightColor.Z = lightIntensity * 0.3f;//B

            // The ambient light is less intensive than the diffuse light in order to make it less dominant
            ambientColor = lightColor * new Vector3(0.2f);
            diffuseColor = lightColor * new Vector3(0.5f);
            terrainShaders?.Bind();
            terrainShaders?.SetVector3Uniform("light.position", _lightPos);
            terrainShaders?.SetVector3Uniform("light.ambient", ambientColor);
            terrainShaders?.SetVector3Uniform("light.diffuse", diffuseColor);
            terrainShaders?.SetVector3Uniform("light.specular", new Vector3(1.0f, 1.0f, 1.0f));



            if (!IsMenuRendered())
                _lightPos = new Vector3(GetPlayer().GetPosition().X,
                    RegionManager.CHUNK_HEIGHT, GetPlayer().GetPosition().Z);
            else
                _lightPos = new Vector3(0, RegionManager.CHUNK_HEIGHT, 0);
            

            SetTerrainShaderUniforms();

            lightingShaders?.Bind();
            sunlightViewMatrix = Matrix4.LookAt(_lightPos, new(0, 0, 0), Vector3.UnitZ);
            lightingShaders?.SetVector3Uniform("light.position", _lightPos);

            if (!IsMenuRendered())
                sunlightViewMatrix = Matrix4.LookAt(_lightPos, GetPlayer().GetPosition(), Vector3.UnitZ);
            else
                sunlightViewMatrix = Matrix4.LookAt(_lightPos, new(0, 0, 0), Vector3.UnitZ);

            lightSpaceMatrix = sunlightViewMatrix * sunlightProjectionMatrix;



            lightingShaders?.SetMatrixUniform("lightViewMatrix", sunlightViewMatrix);
            lightingShaders?.SetMatrixUniform("lightProjMatrix", sunlightProjectionMatrix);
            lightingShaders?.SetMatrixUniform("lightModel", Chunk.GetModelMatrix());

            terrainShaders?.Bind();
            terrainShaders?.SetMatrixUniform("lightViewMatrix", sunlightViewMatrix);
            terrainShaders?.SetMatrixUniform("lightProjMatrix", sunlightProjectionMatrix);
            terrainShaders?.SetMatrixUniform("lightModel", Chunk.GetModelMatrix());
        }

        private KeyboardState previousKeyboardState;
        protected override void OnKeyDown(KeyboardKeyEventArgs e)
        {
            base.OnKeyDown(e);
            KeyboardState current = KeyboardState.GetSnapshot();

            if (!IsMenuRendered()) {
                if (current[Keys.W])// && Math.Sign(GetPlayer().GetForwardDirection().Z) != Math.Sign(GetPlayer().GetBlockedDirection().Z))
                    GetPlayer().MoveForward(1);
                if (current[Keys.S] && Math.Sign(-GetPlayer().GetForwardDirection().Z) != Math.Sign(GetPlayer().GetBlockedDirection().Z)) GetPlayer().MoveForward(-1);
                if (current[Keys.A] && Math.Sign(-GetPlayer().GetRightDirection().X) != Math.Sign(GetPlayer().GetBlockedDirection().X)) GetPlayer().MoveRight(-1);
                if (current[Keys.D] && Math.Sign(GetPlayer().GetRightDirection().X) != Math.Sign(GetPlayer().GetBlockedDirection().X)) GetPlayer().MoveRight(1);
                               
                if (current[Keys.Space] && Math.Sign(GetPlayer().GetBlockedDirection().Y) != 1)
                    player.MoveUp(1);

                if (current[Keys.LeftShift] && Math.Sign(GetPlayer().GetBlockedDirection().Y) != -1)
                    player.MoveUp(-1);

                if (current[Keys.Escape])
                    CursorState = CursorState.Normal;
            }

            previousKeyboardState = current;
        }
        protected override void OnKeyUp(KeyboardKeyEventArgs e)
        {
            base.OnKeyUp(e);
        }

        protected override void OnMouseMove(MouseMoveEventArgs e)
        {
            base.OnMouseMove(e);

        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            if (!IsMenuRendered())
            {
                BlockDetail block = GetPlayer().UpdateViewTarget(out Face playerFacing, out Vector3 blockFace);
                Chunk actionChunk = RegionManager.GetAndLoadGlobalChunkFromCoords((int)block.GetLowerCorner().X, (int)block.GetLowerCorner().Y, (int)block.GetLowerCorner().Z);

                if (e.Button == MouseButton.Left)
                {
                    Console.WriteLine("Add Block: " + block.GetLowerCorner());
                    if (block.IsSurrounded() && !block.IsRendered())
                        actionChunk.AddBlockToChunk(block.GetLowerCorner());
                    else if (block.IsRendered())
                        actionChunk.AddBlockToChunk(block.GetLowerCorner() + blockFace);
                }
                if (e.Button == MouseButton.Right)
                {
                    Console.WriteLine("Remove Block: " + block.GetLowerCorner());
                    actionChunk.RemoveBlockFromChunk(block.GetLowerCorner());
                }

            }
        }

        protected override void OnTextInput(TextInputEventArgs e)
        {
            base.OnTextInput(e);
            _UIController.PressChar((char)e.Unicode);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);

            _UIController.MouseScroll(e.Offset);
        }
        public static long GetMenuSeed()
        {
            return menuSeed;
        }
        private static void RenderUI()
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
                ImGui.Text("IsGrounded: " + GetPlayer().IsPlayerGrounded());

                Face f = Face.ALL;
                BlockDetail block = GetPlayer().UpdateViewTarget(out f, out Vector3 blockface);
                ImGui.Text("Facing Add: " + blockface);
                ImGui.Text("");

                ImGui.PushStyleColor(ImGuiCol.Text, ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f)));
                ImGui.Text("World");
                ImGui.PopStyleColor();

                ImGui.Text("Region: " + GetPlayer().GetRegionWithPlayer().ToString());
                ImGui.Text(GetPlayer().GetChunkWithPlayer().ToString());
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

                ImGui.Text("FPS: " + fps);
                ImGui.Text("Memory: " + Utils.FormatSize(Process.GetCurrentProcess().WorkingSet64) + "/" + Utils.FormatSize(Process.GetCurrentProcess().PrivateMemorySize64));
                ImGui.Text("");

                ImGui.PushStyleColor(ImGuiCol.Text, ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(1.0f, 1.0f, 0.0f, 1.0f)));
                ImGui.Text("Player Matrix");
                ImGui.PopStyleColor();

                ImGui.Text(player.GetViewMatrix().ToString());
                ImGui.End();
            }
        }

        public static bool IsMenuRendered() { return renderMenu; }

        private void RenderMenu()
        {

            //Vertices
            TerrainVertex[] vertexBuffer = menuChunk.GetVertices();

            //Elements
            int[] elementBuffer = menuChunk.GetElements();

            /*==========================================
             * DEPTH RENDERING PRE-PASS
             * ========================================*/


            //Bind FBO and clear depth

            GL.Clear(ClearBufferMask.DepthBufferBit);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, sunlightDepthMapFBO);


            //Check FBO
            FramebufferErrorCode status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
            {
                Console.WriteLine($"Framebuffer error: {status}");
            }
            lightingShaders?.Bind();
            GL.BindVertexArray(vao);


            // Create VBO upload the vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertexBuffer.Length * Unsafe.SizeOf<TerrainVertex>(), vertexBuffer, BufferUsageHint.StaticDraw);

            // Create EBO upload the element buffer;
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, elementBuffer.Length * sizeof(int), elementBuffer, BufferUsageHint.StaticDraw);

            
            GL.DrawElements(PrimitiveType.TriangleStrip, elementBuffer.Length, DrawElementsType.UnsignedInt, 0);



            //Unbind sunlight depth map at the end of frame render
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            /*==========================================
             * COLOR/TEXTURE RENDERING PASS
             * ========================================*/

            //-------------------------Render and Draw Terrain---------------------------------------

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            terrainShaders?.Bind();
            GL.BindVertexArray(vao);


            // Create VBO upload the vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertexBuffer.Length * Unsafe.SizeOf<TerrainVertex>(), vertexBuffer, BufferUsageHint.StaticDraw);

            // Create EBO upload the element buffer;
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, elementBuffer.Length * sizeof(int), elementBuffer, BufferUsageHint.StaticDraw);


            /*==================================
            Drawing
            ====================================*/
            GL.DrawElements(PrimitiveType.TriangleStrip, elementBuffer.Length, DrawElementsType.UnsignedInt, 0);

            //-------------------------Render and Draw Terrain---------------------------------------
        }

 
        //TODO: NEED TO COMBINE ALL DEPTH TEXTURES INTO A SINGLE TEXTURE FOR ALL CHUNKS
        private static void RenderWorld()
        {
           //SetTerrainShaderUniforms();
           // Console.WriteLine(lightSpaceMatrix);
            /*====================================
                Chunk and Region check
            =====================================*/

            //playerChunk will be null when world first loads
            if (globalPlayerChunk == null)
            {
                globalPlayerChunk = new Chunk().Initialize(player.GetChunkWithPlayer().GetLocation().X,//+ RegionManager.CHUNK_BOUNDS,
                     player.GetChunkWithPlayer().GetLocation().Y, player.GetChunkWithPlayer().GetLocation().Z);
                player.SetPosition(new(player.GetPosition().X, player.GetPosition().Y + 10, player.GetPosition().Z));
            }

            //Updates the chunks to render when the player has moved into a new chunk
            Dictionary<string, Chunk> chunksToRender = ChunkCache.UpdateChunkCache();
            if (!player.GetChunkWithPlayer().Equals(globalPlayerChunk))
            {
                RegionManager.UpdateVisibleRegions();
                globalPlayerChunk = player.GetChunkWithPlayer();
                ChunkCache.SetPlayerChunk(globalPlayerChunk);

                chunksToRender = ChunkCache.UpdateChunkCache();

                //Updates the regions when player moves into different region
                if (!player.GetRegionWithPlayer().Equals(globalPlayerRegion))
                    globalPlayerRegion = player.GetRegionWithPlayer();
            }
            //=========================================================================

            //Per chunk primitive information calculated in thread pool and later sent to GPU for drawing
            ConcurrentBag<TerrainRenderTask> terrainRenderTasks = [];

            //TODO: need all of the vertex and element info in one place for rendering. but need to do it in a way thats not slow
            CountdownEvent countdown = new(chunksToRender.Count);
            foreach (KeyValuePair<string, Chunk> c in chunksToRender)
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(delegate (object state)
                {
                    terrainRenderTasks.Add(c.Value.GenerateRenderData());
                    countdown.Signal();
                }));
            }
            countdown.Wait();

            /*======================================================
            Getting vertex and element information for rendering
            ========================================================*/

            foreach (TerrainRenderTask renderTask in terrainRenderTasks)
            {
                TerrainVertex[] vertexBuffer = renderTask.GetVertexData();
                int[] elementBuffer = renderTask.GetElementData();

                if (vertexBuffer.Length > 0 && elementBuffer.Length > 0)
                {

                    /*==========================================
                     * DEPTH RENDERING PRE-PASS
                     * ========================================*/
                    //Bind FBO and clear depth

                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, sunlightDepthMapFBO);


                    //Check FBO
                    FramebufferErrorCode status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
                    if (status != FramebufferErrorCode.FramebufferComplete)
                    {
                        Console.WriteLine($"Framebuffer error: {status}");
                    }
                    lightingShaders?.Bind();
                    GL.BindVertexArray(vao);


                    // Create VBO upload the vertex buffer
                    GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
                    GL.BufferData(BufferTarget.ArrayBuffer, vertexBuffer.Length * Unsafe.SizeOf<TerrainVertex>(), vertexBuffer, BufferUsageHint.StaticDraw);

                    // Create EBO upload the element buffer;
                    GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
                    GL.BufferData(BufferTarget.ElementArrayBuffer, elementBuffer.Length * sizeof(int), elementBuffer, BufferUsageHint.StaticDraw);


                    GL.DrawElements(PrimitiveType.TriangleStrip, elementBuffer.Length, DrawElementsType.UnsignedInt, 0);



                    //Unbind sunlight depth map at the end of frame render
                    GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

                    /*==========================================
                     * COLOR/TEXTURE RENDERING PASS
                     * ========================================*/

                    //-------------------------Render and Draw Terrain---------------------------------------

                    terrainShaders?.Bind();
                    GL.BindVertexArray(vao);


                    // Create VBO upload the vertex buffer
                    GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
                    GL.BufferData(BufferTarget.ArrayBuffer, vertexBuffer.Length * Unsafe.SizeOf<TerrainVertex>(), vertexBuffer, BufferUsageHint.StaticDraw);

                    // Create EBO upload the element buffer;
                    GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
                    GL.BufferData(BufferTarget.ElementArrayBuffer, elementBuffer.Length * sizeof(int), elementBuffer, BufferUsageHint.StaticDraw);


                    /*==================================
                    Drawing
                    ====================================*/
                    GL.DrawElements(PrimitiveType.TriangleStrip, elementBuffer.Length, DrawElementsType.UnsignedInt, 0);

                    //-------------------------Render and Draw Terrain---------------------------------------
                }
            }
           // foreach (TerrainRenderTask renderTask in terrainRenderTasks)
           // {
           //     //Elements
           //     int[] elementBuffer = renderTask.GetElementData();
           // 
           //     //Vertices
           //     TerrainVertex[] vertexBuffer = renderTask.GetVertexData();
           // 
           // 
           //     int vbo = renderTask.GetVbo();
           //     int ebo = renderTask.GetEbo();
           //     int vao = renderTask.GetVao();
           //     GL.BindVertexArray(vao);
           // 
           //     int lightvbo = GL.GenBuffer();
           //     int ligghtEbo = GL.GenBuffer();
           // 
           //     /*=====================================
           //     Vertex attribute definitions for shaders
           //     ======================================*/
           //     int posSize = 3;
           //     int layerSize = 1;
           //     int coordSize = 1;
           //     int lightSize = 1;
           //     int normalSize = 3;
           //     int faceSize = 1;
           //     int typeSize = 1;
           // 
           //     // Position
           //     GL.VertexAttribPointer(0, posSize, VertexAttribPointerType.Float, false, Unsafe.SizeOf<TerrainVertex>(), 0);
           //     GL.EnableVertexAttribArray(0);
           // 
           //     // Texture Layer
           //     GL.VertexAttribPointer(1, layerSize, VertexAttribPointerType.Float, false, Unsafe.SizeOf<TerrainVertex>(), (posSize) * sizeof(float));
           //     GL.EnableVertexAttribArray(1);
           // 
           //     // Texture Coordinates
           //     GL.VertexAttribPointer(2, coordSize, VertexAttribPointerType.Float, false, Unsafe.SizeOf<TerrainVertex>(), (posSize + layerSize) * sizeof(float));
           //     GL.EnableVertexAttribArray(2);
           // 
           //     // Light
           //     GL.VertexAttribPointer(3, lightSize, VertexAttribPointerType.Float, false, Unsafe.SizeOf<TerrainVertex>(), (posSize + layerSize + coordSize) * sizeof(float));
           //     GL.EnableVertexAttribArray(3);
           // 
           //     // Normals
           //     GL.VertexAttribPointer(4, normalSize, VertexAttribPointerType.Float, false, Unsafe.SizeOf<TerrainVertex>(), (posSize + layerSize + coordSize + lightSize) * sizeof(float));
           //     GL.EnableVertexAttribArray(4);
           // 
           //     // Block type
           //     GL.VertexAttribPointer(5, typeSize, VertexAttribPointerType.Float, false, Unsafe.SizeOf<TerrainVertex>(), (posSize + layerSize + coordSize + lightSize + faceSize + normalSize) * sizeof(float));
           //     GL.EnableVertexAttribArray(5);
           // 
           //     // Block face
           //     GL.VertexAttribPointer(6, faceSize, VertexAttribPointerType.Float, false, Unsafe.SizeOf<TerrainVertex>(), (posSize + layerSize + coordSize + lightSize + faceSize + normalSize + typeSize) * sizeof(float));
           //     GL.EnableVertexAttribArray(6);
           //      
           //      //Sends chunk data to GPU for drawing
           //      if (vertexBuffer.Length > 0 && elementBuffer.Length > 0)
           //      {
           //   
           //   
           //          /*==========================================
           //           * DEPTH RENDERING PRE-PASS
           //           * ========================================*/
           //   
           //   
           //          //Bind FBO and clear depth
           //          GL.Clear(ClearBufferMask.DepthBufferBit);
           //          GL.BindFramebuffer(FramebufferTarget.Framebuffer, sunlightDepthMapFBO);
           //          lightingShaders?.Bind();
           //   
           //          //Check FBO
           //          FramebufferErrorCode status1 = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
           //          if (status1 != FramebufferErrorCode.FramebufferComplete)
           //          {
           //              Console.WriteLine($"Framebuffer error: {status1}");
           //          }
           //   
           //          GL.BindVertexArray(vao);
           //   
           //   
           //          // Create VBO upload the vertex buffer
           //          GL.BindBuffer(BufferTarget.ArrayBuffer, lightVbo);
           //          GL.BufferData(BufferTarget.ArrayBuffer, vertexBuffer.Length * Unsafe.SizeOf<TerrainVertex>(), vertexBuffer, BufferUsageHint.StaticDraw);
           //   
           //          // Create EBO upload the element buffer;
           //          GL.BindBuffer(BufferTarget.ElementArrayBuffer, lightEbo);
           //          GL.BufferData(BufferTarget.ElementArrayBuffer, elementBuffer.Length * sizeof(int), elementBuffer, BufferUsageHint.StaticDraw);
           //   
           //   
           //          GL.DrawElements(PrimitiveType.TriangleStrip, elementBuffer.Length, DrawElementsType.UnsignedInt, 0);
           //   
           //   
           //   
           //          //Unbind sunlight depth map at the end of frame render
           //          GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
           //   
           //          /*==========================================
           //           * COLOR/TEXTURE RENDERING PASS
           //           * ========================================*/
           //          GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
           //          terrainShaders?.Bind();
           //   
           //          // Create VBO upload the vertex buffer
           //          GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
           //          GL.BufferData(BufferTarget.ArrayBuffer, vertexBuffer.Length * Unsafe.SizeOf<TerrainVertex>(), vertexBuffer, BufferUsageHint.StaticDraw);
           //   
           //   
           //   
           //          // Create EBO upload the element buffer
           //          GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
           //          GL.BufferData(BufferTarget.ElementArrayBuffer, elementBuffer.Length * sizeof(int), elementBuffer, BufferUsageHint.StaticDraw);
           //   
           //   
           //          /*==================================
           //          Drawing
           //          ====================================*/
           //   
           //          GL.DrawElements(PrimitiveType.TriangleStrip, elementBuffer.Length, DrawElementsType.UnsignedInt, 0);
           //      }
           //  }
                
            
        
             RenderBlockTarget();
        }

        public static void RenderBlockTarget()
        {

            BlockDetail block = GetPlayer().UpdateViewTarget(out _, out Vector3 blockFace);
            if (block.IsSurrounded() && !block.IsRendered())
            {
                BlockModel model = ModelLoader.GetModel(BlockType.TARGET_BLOCK);

                /*==================================
                Render View Target Block Outline
                ====================================*/
                TerrainVertex[] viewBlockVertices = GetPlayer().GetViewTargetForRendering();

                if (viewBlockVertices.Length > 0)
                {

                    int viewVBO = GL.GenBuffer();

                    // Bind and upload vertex data
                    GL.BindBuffer(BufferTarget.ArrayBuffer, viewVBO);
                    GL.BufferData(BufferTarget.ArrayBuffer, viewBlockVertices.Length * Unsafe.SizeOf<TerrainVertex>(), viewBlockVertices, BufferUsageHint.StaticDraw);


                    //Draw the view target outline
                    terrainShaders.Bind();
                    GL.DrawElements(PrimitiveType.TriangleStrip, 24, DrawElementsType.UnsignedInt, 0);

                }
            }
          //else if (block.IsSurrounded() && block.IsRendered())
          //{
          //    BlockModel model = ModelLoader.GetModel(BlockType.TARGET_BLOCK);
          //
          //    /*==================================
          //    Render View Target Block Outline
          //    ====================================*/
          //
          //    Vertex[] viewBlockVertices = GetPlayer().GetViewTargetForRendering();
          //
          //    //Add offset to prevent placing a block in a position that already contains one
          //    for (int i = 0; i < viewBlockVertices.Length; i++)
          //    {
          //        viewBlockVertices[i].SetVector(viewBlockVertices[i].GetVector() + blockFace);
          //    }
          //
          //    if (viewBlockVertices.Length > 0)
          //    {
          //
          //        int viewVBO = GL.GenBuffer();
          //
          //        // Bind and upload vertex data
          //        GL.BindBuffer(BufferTarget.ArrayBuffer, viewVBO);
          //        GL.BufferData(BufferTarget.ArrayBuffer, viewBlockVertices.Length * Unsafe.SizeOf<Vertex>(), viewBlockVertices, BufferUsageHint.StaticDraw);
          //
          //
          //        //Draw the view target outline
          //        GL.DrawElements(PrimitiveType.TriangleStrip, 24, DrawElementsType.UnsignedInt, 0);
          //
          //    }
          //}
        }


        public static Chunk GetGlobalChunk()
        {
            if (globalPlayerChunk == null)
                return new Chunk().Initialize(0, 0, 0);
            return globalPlayerChunk;
        }
        private static float[] GetCrosshair()
        {
            return [
                (float) screenWidth / 2 - 13.5f, (float) screenHeight / 2 - 13.5f, 0, 0,
                (float) screenWidth / 2 + 13.5f, (float) screenHeight / 2 - 13.5f, 1, 0,
                (float) screenWidth / 2 + 13.5f, (float) screenHeight / 2 + 13.5f, 1, 1,

                (float) screenWidth / 2 - 13.5f, (float) screenHeight / 2 - 13.5f, 0, 0,
                (float) screenWidth / 2 - 13.5f, (float) screenHeight / 2 + 13.5f, 1, 0,
                (float) screenWidth / 2 + 13.5f, (float) screenHeight / 2 + 13.5f, 1, 1,
            ];
        }
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            terrainShaders?.Cleanup();
            crosshairShader?.Cleanup();
            TextureLoader.Unbind();
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);

            //Update ortho matrix for crosshair on window resize
            terrainShaders.SetMatrixUniform("crosshairOrtho", Matrix4.CreateOrthographic(screenWidth, screenHeight, 0.1f, 10f));

            // Update the opengl viewport
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);

            Matrix4 pMatrix = Matrix4.CreatePerspectiveFieldOfView(FOV, (float)e.Width / e.Height, NEAR, FAR);
            terrainShaders?.SetMatrixUniform("projectionMatrix", pMatrix);

            // Tell ImGui of the new size
            _UIController.WindowResized(ClientSize.X, ClientSize.Y);
        }

        public static Player GetPlayer() {
            player ??= new();

            return player;
        }
        public static RegionManager GetWorld()
        {
            return loadedWorld;
        }
        public static ShaderProgram GetShaders()
        {
            return terrainShaders;
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
