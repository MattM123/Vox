using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Vox.AssetManagement;
using Vox.Enums;
using Vox.Genesis;
using Vox.Model;
using Vox.Rendering;
using Vox.UI;
using BlendingFactor = OpenTK.Graphics.OpenGL4.BlendingFactor;
using BufferTarget = OpenTK.Graphics.OpenGL4.BufferTarget;
using ClearBufferMask = OpenTK.Graphics.OpenGL4.ClearBufferMask;
using DebugProc = OpenTK.Graphics.OpenGL4.DebugProc;
using DebugSeverity = OpenTK.Graphics.OpenGL4.DebugSeverity;
using DebugSource = OpenTK.Graphics.OpenGL4.DebugSource;
using DebugType = OpenTK.Graphics.OpenGL4.DebugType;
using DepthFunction = OpenTK.Graphics.OpenGL4.DepthFunction;
using DrawBufferMode = OpenTK.Graphics.OpenGL4.DrawBufferMode;
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


namespace Vox
{
    public partial class Window(GameWindowSettings windowSettings, NativeWindowSettings nativeSettings) : GameWindow(windowSettings, nativeSettings)
    {
        ImGuiController _UIController;
        public static readonly int screenWidth = Monitors.GetPrimaryMonitor().ClientArea.Size.X;
        public static readonly int screenHeight = Monitors.GetPrimaryMonitor().ClientArea.Size.Y - 100;

        private static bool renderMenu = true;
        private static readonly string appFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\.voxelGame\\";
        private static ShaderProgram? terrainShaders = null;
        private static ShaderProgram? lightingShaders = null;
        private static ShaderProgram? crosshairShader = null;
        private static ShaderProgram? debugShaders = null;
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
        private static List<Chunk> menuChunks = [];
        private static long menuSeed;
        private static Vector3 _lightPos = new(0.0f, RegionManager.CHUNK_HEIGHT + 100, 0.0f);
        private static int crosshairTex;
        private static int sunlightDepthMapFBO;
        private static int sunlightDepthMap;
        private static Matrix4 pMatrix;
        private Matrix4 sunlightProjectionMatrix;
        private Matrix4 sunlightViewMatrix;
        private static Matrix4 lightSpaceMatrix;
        private static int SSBOhandle;
        private static IntPtr SSBOPtr;
        private static int _nextFaceIndex;
        private static int SSBOSize;

        //used for player and lighting projection matrices
        private static float FAR = 1000.0f;
        private static float NEAR = 1f;

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
            debugShaders = new();

            //Generate menu chunk seed
            byte[] buffer = new byte[8];
            RandomNumberGenerator.Fill(buffer); // Fills the buffer with random bytes
            menuSeed = BitConverter.ToInt64(buffer, 0);


            //Load textures and models

            //crosshairTex = TextureLoader.LoadSingleTexture(Path.Combine(assets, "Textures", "Crosshair_06.png"));

            ModelLoader.LoadModels();

            Title += ": OpenGL Version: " + GL.GetString(StringName.Version);

            _UIController = new ImGuiController(ClientSize.X, ClientSize.Y);

            Directory.CreateDirectory(appFolder + "worlds");
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.Enable(EnableCap.LineSmooth);
            GL.Hint(HintTarget.LineSmoothHint, HintMode.Nicest);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.Enable(EnableCap.Blend);

         //   GL.Enable(EnableCap.CullFace);
          // GL.CullFace(CullFaceMode.Back);

            GL.DebugMessageCallback(DebugMessageDelegate, IntPtr.Zero);
            GL.Enable(EnableCap.DebugOutput);

            //Enable primitive restart
            //   GL.Enable(EnableCap.PrimitiveRestart);
            // GL.PrimitiveRestartIndex(primRestart);

            //============================
            //Shader Compilation and Setup
            //============================
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
            "                            %%%%                            \n" +
            "                          %%%%%%%%                          \n" +
            "                         %%%%%%%%%%                         \n" +
            "                       %%%%%%%%%%%%%%                       \n" +
            "                     %%%%%%%%%%%%%%%%%%                     \n" +
            "                   %%%%%%%%%%%%%%%%%%%%%%                   \n" +
            "                 %%%%%%%%%%%%%%%%%%%%%%%%%%                 \n" +
            "               @%%%%%%%%%%%%%%%%%%%%%%%%%%%%%               \n" +
            "             @%%%%%%%%%%%%%%%%%%%%%%%%%%%%% +-=             \n" +
            "           @%%%%%%%%%%%%%%%%%%%%%%%%%%%%% *-::::=           \n" +
            "          %%%%%%%%%%%%%%%%%%%%%%%%%%%%%#-:::::::-=          \n" +
            "        %%%%%%%%%%%%%%%%%%%%%%%%%%%%%#=:::::::::::-=        \n" +
            "      %%%%%%%%%%%%%%%%%%%%%%%%%%%%%% +:::::::::::::::-=     \n" +
            "    @%%%%%%%%%%%%%%%%%%%%%%%%%%%%% *:::::::::::::::::::=    \n" +
            "  @%%%%%%%%%%%%%%%%%%%%%%%%%%%%% *-::::::::::::::::::::::=@ \n" +
            " %%%%%%%%%%%%%%%%%%%%%%%%%%%%%#-:::::::::::::::::::::::::-= \n" +
            "@%%%%%%%%%%%%%%%%%%%%%%%%%%%%%=:::::::::::::::::::::::::::-+\n" +
            "  %%%%%%%%%%%%%%%%%%%%%%%%%%%%%#-:::::::::::::::::::::::-=  \n" +
            "    %%%%%%%%%%%%%%%%%%%%%%%%%%%%% *-::::::::::::::::::::=   \n" +
            "     @%%%%%%%%%%%%%%%%%%%%%%%%%%%%% *-::::::::::::::::=     \n" +
            "       @%%%%%%%%%%%%%%%%%%%%%%%%%%%%% +:::::::::::::= +     \n" +
            "         %%%%%%%%%%%%%%%%%%%%%%%%%%%%%#+:::::::::=+         \n" +
            "           %%%%%%%%%%%%%%%%%%%%%%%%%%%%%#=:::::-+           \n" +
            "             %%%%%%%%%%%%%%%%%%%%%%%%%%%%%#=:-+             \n" +
            "               %%%%%%%%%%%%%%%%%%%%%%%%%%%%%#@              \n" +
            "                @%%%%%%%%%%%%%%%%%%%%%%%%%%@                \n" +
            "                  @%%%%%%%%%%%%%%%%%%%%%%@                  \n" +
            "                    %%%%%%%%%%%%%%%%%%%%                    \n" +
            "                      %%%%%%%%%%%%%%%%       LYGIA.xyz      \n" +
            "                        %%%%%%%%%%%%   Processing Shaders...\n" +
            "                         @%%%%%%%%@                         \n" +
            "                           @%%%%@                           \n" +
            "                             %@                             \n");
            Console.ResetColor();

            //-----------------------Terrain shaders---------------------------------
            //Load main vertex shader from file
            string vertexTerrainShaderSource = ProcessShaderIncludes("..\\..\\..\\Rendering\\VertexTerrainShader.glsl");
            terrainShaders.CreateVertexShader("VertexTerrainShader", vertexTerrainShaderSource);

            // Load main fragment shader from file
            string fragmentTerrainShaderSource = ProcessShaderIncludes("..\\..\\..\\Rendering\\FragTerrainShader.glsl");
            terrainShaders.CreateFragmentShader("FragTerrainShader", fragmentTerrainShaderSource);
            //-----------------------Terrain shaders---------------------------------

            //-----------------------Lihgting shaders---------------------------------
            //Load main vertex shader from file
            string vertexLightingShaderSource = ShaderProgram.LoadShaderFromFile("..\\..\\..\\Rendering\\VertexDepthShader.glsl");
            lightingShaders.CreateVertexShader("VertexDepthShader", vertexLightingShaderSource);

            // Load main fragment shader from file
            string fragmentLightingSource = ShaderProgram.LoadShaderFromFile("..\\..\\..\\Rendering\\FragDepthShader.glsl");
            lightingShaders.CreateFragmentShader("FragDepthShader", fragmentLightingSource);

            //-----------------------Lihgting shaders---------------------------------

            //Load crosshair vertex shaders
            string vertexCrosshairSource = ShaderProgram.LoadShaderFromFile("..\\..\\..\\Rendering\\Vertex_Crosshair.glsl");
            crosshairShader.CreateVertexShader("Vertex_Crosshair", vertexCrosshairSource);

            //Load crosshair frag shaders
            string fragmentCrosshairSource = ShaderProgram.LoadShaderFromFile("..\\..\\..\\Rendering\\Frag_Crosshair.glsl");
            crosshairShader.CreateFragmentShader("Frag_Crosshair", fragmentCrosshairSource);

            //-----------------------Debug shaders---------------------------------
            string geometryShaderCode = ShaderProgram.LoadShaderFromFile("..\\..\\..\\Rendering\\DebugGeoShader.glsl");
            debugShaders.CreateGeometryShader("DebugShader", geometryShaderCode);

            string vertexDebugShaderCode = ProcessShaderIncludes("..\\..\\..\\Rendering\\VertexTerrainShader.glsl");
            debugShaders.CreateVertexShader("VertexTerrainShader", vertexDebugShaderCode);
            //-----------------------Debug shaders---------------------------------

            debugShaders.Link();
            terrainShaders.Link();
            lightingShaders.Link();

            //Sunlight frame buffer for shadow map
            sunlightDepthMapFBO = GL.GenFramebuffer();
            sunlightDepthMap = GL.GenTexture();

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, sunlightDepthMap);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent32,
                 4096, 4096, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
            GL.Viewport(0, 0, 4096, 4096);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareFunc, (int)DepthFunction.Less);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareMode, (int)TextureCompareMode.CompareRefToTexture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

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

            float dist = RegionManager.CHUNK_BOUNDS * RegionManager.GetRenderDistance();
            sunlightProjectionMatrix = Matrix4.CreateOrthographicOffCenter(-dist, dist, -dist, dist, 1f, dist * 2);

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

            terrainShaders.CreateUniform("targetTexLayer");
            terrainShaders.CreateUniform("lightSpaceMatrix");
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

            debugShaders.CreateUniform("playerMin");
            debugShaders.CreateUniform("playerMax");
            debugShaders.CreateUniform("viewMatrix");
            debugShaders.CreateUniform("projectionMatrix");

            // This is where we change the lights color over time using the sin function
            float time = DateTime.Now.Second + DateTime.Now.Millisecond / 1000f;
            lightColor.X = 1.4f; //(MathF.Sin(time * 2.0f) + 1) / 2f;
            lightColor.Y = 1f;//(MathF.Sin(time * 0.7f) + 1) / 2f;
            lightColor.Z = 1f; //(MathF.Sin(time * 1.3f) + 1) / 2f;


            // The ambient light is less intensive than the diffuse light in order to make it less dominant
            ambientColor = lightColor * new Vector3(0.2f);
            diffuseColor = lightColor * new Vector3(0.5f);


        /*======================================
         Block face SSBO instancing setup
         =======================================*/

            //BlockFace SSBO upload
            SSBOhandle = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, SSBOhandle);

            // SSBO Size based on nrender distance
            int blockFacesPerBlock = 6;
            int maxBlocksPerChunk = (int)Math.Pow(RegionManager.CHUNK_BOUNDS, 3);
            int maxChunksInCache = (int)Math.Pow(RegionManager.GetRenderDistance(), 3);
            SSBOSize =((maxBlocksPerChunk * maxChunksInCache * blockFacesPerBlock) * Marshal.SizeOf<BlockFaceInstance>());

            //Creates ssbo buffer
            GL.BufferStorage(BufferTarget.ShaderStorageBuffer, SSBOSize, IntPtr.Zero, BufferStorageFlags.MapPersistentBit | BufferStorageFlags.MapCoherentBit | BufferStorageFlags.MapWriteBit);

            //Map binding for shader to use
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, SSBOhandle);

            //Creates pointer to SSBO buffer
            SSBOPtr = GL.MapBufferRange(
                BufferTarget.ShaderStorageBuffer,
                IntPtr.Zero,
                SSBOSize,
                BufferAccessMask.MapWriteBit |
                BufferAccessMask.MapPersistentBit |
                BufferAccessMask.MapCoherentBit
            );
            menuChunks.Add(RegionManager.GetAndLoadGlobalChunkFromCoords(0, 176, 0));
            menuChunks.Add(RegionManager.GetAndLoadGlobalChunkFromCoords(0, 192, 0));
            menuChunks.Add(RegionManager.GetAndLoadGlobalChunkFromCoords(0, 208, 0));

            foreach (Chunk c in menuChunks)
            {
                c.GenerateRenderData();
            }
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {

            base.OnRenderFrame(e);


            /*==============================
            Update UI input and config
            ===============================*/
            _UIController.Update(this, (float)e.Time);


            GL.ClearColor(0.5f, 0.8f, 1.0f, 0.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);

            //Attach depth texture to frame buffer         
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, sunlightDepthMapFBO);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, sunlightDepthMap, 0);
            GL.DrawBuffer(DrawBufferMode.None);
            GL.ReadBuffer(ReadBufferMode.None);

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
                terrainShaders?.SetIntFloatUniform("targetTexLayer", AssetLookup.GetTextureValue("target.png"));
                terrainShaders?.SetVector3Uniform("targetVertex", GetPlayer().UpdateViewTarget(out _, out _, out _));
                terrainShaders?.SetVector3Uniform("playerMin", GetPlayer().GetBoundingBox()[0]);
                terrainShaders?.SetVector3Uniform("playerMax", GetPlayer().GetBoundingBox()[1]);
                terrainShaders?.SetMatrixUniform("viewMatrix", GetPlayer().GetViewMatrix());

                terrainShaders?.SetVector3Uniform("playerMin", Player.playerMin);
                terrainShaders?.SetVector3Uniform("playerMax", Player.playerMax);

            }
            terrainShaders?.SetVector3Uniform("forwardDir", GetPlayer().GetForwardDirection());
            terrainShaders?.SetVector3Uniform("playerPos", GetPlayer().GetPosition());


            terrainShaders?.SetIntFloatUniform("renderDistance", RegionManager.GetRenderDistance());

            terrainShaders?.SetMatrixUniform("chunkModelMatrix", Chunk.GetModelMatrix());


            terrainShaders?.SetIntFloatUniform("isMenuRendered", 1);
            terrainShaders?.SetVector3Uniform("playerMin", GetPlayer().GetBoundingBox()[0]);
            terrainShaders?.SetVector3Uniform("playerMax", GetPlayer().GetBoundingBox()[1]);
          

            //material uniforms
            //terrainShaders?.SetVector3Uniform("material.ambient", new Vector3(1.0f, 0.5f, 0.31f));
            //terrainShaders?.SetVector3Uniform("material.diffuse", new Vector3(1.5f, 1.5f, 1.5f));
            //terrainShaders?.SetVector3Uniform("material.specular", new Vector3(0.5f, 0.5f, 0.5f));
            //terrainShaders?.SetIntFloatUniform("material.shininess", 32.0f);
        }

        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);

            //Update player and cursor state
            if (!IsMenuRendered())
            {
                GetPlayer().Update((float)args.Time);

                if (!ImGuiHelper.SHOW_BLOCK_COLOR_PICKER)
                {
                    CursorState = CursorState.Grabbed;
                    Player.SetLookDir(MouseState.Y, MouseState.X);
                }
                else
                    CursorState = CursorState.Normal;

                
            }

            /*==================================
             Ambient lighting update
            ====================================*/
            //update light color each frame
            float dayLengthInSeconds = 60.0f;  
            float timeOfDay = (DateTime.Now.Second + DateTime.Now.Millisecond / 1000f) % dayLengthInSeconds;
            float normalizedTime = timeOfDay / dayLengthInSeconds;  // Normalized time between 0 (start of day) and 1 (end of day)
            float dayCycle = MathF.Sin(normalizedTime * MathF.PI * 2);  // Sine wave oscillates between -1 and 1 over one full cycle
            float angle = normalizedTime * MathF.PI * 2;
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



            //Move light in a circlular path around the player to simulate sunlight
            //x = h + r⋅cos(θ)
            //y = k + r⋅sin(θ)
            float radius = RegionManager.CHUNK_BOUNDS * RegionManager.GetRenderDistance() * 2f;
            Vector3 playerPos = GetPlayer().GetPosition();
            float x = playerPos.X + radius * MathF.Cos(angle);
            float z = playerPos.Z + radius * MathF.Sin(angle);
            float y = playerPos.Y + radius * 0.5f * MathF.Sin(angle);
            
            _lightPos = new Vector3(x, y, z);



            SetTerrainShaderUniforms();

            lightingShaders?.Bind();
            lightingShaders?.SetVector3Uniform("light.position", _lightPos);

            if (!IsMenuRendered())
                sunlightViewMatrix = Matrix4.LookAt(_lightPos, new(0, 0, 0), Vector3.UnitY);
            else
                sunlightViewMatrix = Matrix4.LookAt(_lightPos, GetPlayer().GetPosition(), Vector3.UnitY);

            float dist1 = RegionManager.CHUNK_BOUNDS * RegionManager.GetRenderDistance();
            sunlightProjectionMatrix = Matrix4.CreateOrthographicOffCenter(-dist1, dist1, -dist1, dist1 , 1f, dist1);

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

            if (float.IsNaN(GetPlayer().GetBlockedDirection().X))
                return;

            if (!IsMenuRendered()) {
                if (current[Keys.W])// && Math.Sign(GetPlayer().GetForwardDirection().Z) != Math.Sign(GetPlayer().GetBlockedDirection().Z))
                    GetPlayer().MoveForward(1);
                if (current[Keys.S] && Math.Sign(-GetPlayer().GetForwardDirection().Z) != Math.Sign(GetPlayer().GetBlockedDirection().Z)) GetPlayer().MoveForward(-1);
                if (current[Keys.A] && Math.Sign(-GetPlayer().GetRightDirection().X) != Math.Sign(GetPlayer().GetBlockedDirection().X)) GetPlayer().MoveRight(-1);
                if (current[Keys.D] && Math.Sign(GetPlayer().GetRightDirection().X) != Math.Sign(GetPlayer().GetBlockedDirection().X)) GetPlayer().MoveRight(1);
                               
                if (current[Keys.Space] && Math.Sign(GetPlayer().GetBlockedDirection().Y) != 1)
                    player.MoveUp(3);

                if (current[Keys.LeftShift] && Math.Sign(GetPlayer().GetBlockedDirection().Y) != -1)
                    player.MoveUp(-1);

                if (current[Keys.Escape])
                    CursorState = CursorState.Normal;

                //Color Picker
                Vector3 target = GetPlayer().UpdateViewTarget(out _, out _, out Vector3 blockSpace);
                Chunk actionChunk = RegionManager.GetAndLoadGlobalChunkFromCoords(target);
                Vector3i idx = RegionManager.GetChunkRelativeCoordinates(target);
                if (current[Keys.C] && (BlockType) actionChunk.blockData[idx.X, idx.Y, idx.Z] == BlockType.LAMP_BLOCK)
                {
                    if (!ImGuiHelper.SHOW_BLOCK_COLOR_PICKER)
                    {
                        ImGuiHelper.SHOW_BLOCK_COLOR_PICKER = true;
                        CursorState = CursorState.Normal;
                       // Cursor
                    }
                    else
                    {
                        ImGuiHelper.SHOW_BLOCK_COLOR_PICKER = false;
                      //  Player.SetLookDir(MouseState.Y, MouseState.X);
                        //   CursorState = CursorState.Grabbed;
                    }
                }
                if (current[Keys.C] && (BlockType)actionChunk.blockData[idx.X, idx.Y, idx.Z] != BlockType.LAMP_BLOCK)
                {
                    if (ImGuiHelper.SHOW_BLOCK_COLOR_PICKER)
                    {
                        ImGuiHelper.SHOW_BLOCK_COLOR_PICKER = false;
                        //  CursorState = CursorState.Grabbed;
                    //    Player.SetLookDir(MouseState.Y, MouseState.X);
                    }
                } 


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
                Vector3 block = GetPlayer().UpdateViewTarget(out BlockFace playerFacing, out Vector3 blockFace, out Vector3 blockSpace);

                if (e.Button == MouseButton.Left && !ImGuiHelper.SHOW_BLOCK_COLOR_PICKER)
                {
                    RegionManager.AddBlockToChunk(blockSpace, GetPlayer().GetPlayerBlockType(), new(0,0,0));

                }
                if (e.Button == MouseButton.Right && !ImGuiHelper.SHOW_BLOCK_COLOR_PICKER)
                {
                    RegionManager.RemoveBlockFromChunk(block);
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
        private void RenderUI()
        {
            ImGuiIOPtr ioptr = ImGui.GetIO();

            if (renderMenu)
            {
                ImGuiHelper.ShowWorldMenu(ioptr);
            }
            else
            {
              //  ImGuiHelper.ShowDebugMenu(ioptr);
            }

            //Color Picker
            KeyboardState current = KeyboardState.GetSnapshot();
            Vector3 target = GetPlayer().UpdateViewTarget(out _, out _, out _);

            if (ImGuiHelper.SHOW_BLOCK_COLOR_PICKER) {

                ImGuiHelper.ShowBlockColorPicker(target);
            }
        }
       

        public static bool IsMenuRendered() { return renderMenu; }

        public static void SetMenuRendered(bool val) { renderMenu = val; }

        private void RenderMenu()
        {
            int vaoo = GL.GenVertexArray();

            /*==========================================
             * DEPTH RENDERING PRE-PASS
             * ========================================*/
            GL.Viewport(0, 0, 4096, 4096);

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
            GL.BindVertexArray(vaoo);

            GL.DrawArraysInstanced(
                PrimitiveType.TriangleStrip,  // Drawing a triangle strip
                0,                            // Start from the first vertex in the base geometry
                4,                            // 4 vertices per face (for triangle strip)
                _nextFaceIndex                // Instance count (number of faces to draw)
            );


            //Unbind sunlight depth map at the end of frame render
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            /*==========================================
             * COLOR/TEXTURE RENDERING PASS
             * ========================================*/
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
            //-------------------------Render and Draw Terrain---------------------------------------

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            terrainShaders?.Bind();



            /*==================================
            Drawing
            ====================================*/
            GL.DrawArraysInstanced(
                PrimitiveType.TriangleStrip,  // Drawing a triangle strip
                0,                            // Start from the first vertex in the base geometry
                4,                            // 4 vertices per face (for triangle strip)
                _nextFaceIndex                // Instance count (number of faces to draw)
            );

            //-------------------------Render and Draw Terrain---------------------------------------
            GL.BindVertexArray(0);
        }

        public static RegionManager GetLoadedWorld()
        {
            return loadedWorld;
        }
        public static void SetLoadedWorld(RegionManager rm)
        {
            loadedWorld = rm;
        }
        public static string GetAppFolder()
        {
            return appFolder;   
        }
        private void RenderWorld()
        {
            //Remeber to clear cache each frame 
            ChunkCache.ClearChunkCache();

            //Recalculate SSBO size
            int blockFacesPerBlock = 6;
            int maxBlocksPerChunk = (int)Math.Pow(RegionManager.CHUNK_BOUNDS, 3);
            int maxChunksInCache = (int)Math.Pow(RegionManager.GetRenderDistance(), 3);

            //If SSBO size changed (i.e render distance was increased or decreased) 
            if (Marshal.SizeOf<BlockFaceInstance>() * (maxBlocksPerChunk * maxChunksInCache * blockFacesPerBlock) != SSBOSize)
            {
                Console.WriteLine("Size Change");
                SSBOSize = Marshal.SizeOf<BlockFaceInstance>() * (maxBlocksPerChunk * maxChunksInCache * blockFacesPerBlock);
            
                //Creates ssbo buffer
                GL.BufferStorage(BufferTarget.ShaderStorageBuffer, SSBOSize, IntPtr.Zero, BufferStorageFlags.MapPersistentBit | BufferStorageFlags.MapCoherentBit | BufferStorageFlags.MapWriteBit);
            
                //Map binding for shader to use
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, SSBOhandle);
            
                //Creates pointer to SSBO buffer
                SSBOPtr = GL.MapBufferRange(
                    BufferTarget.ShaderStorageBuffer,
                    IntPtr.Zero,
                    SSBOSize,
                    BufferAccessMask.MapWriteBit |
                    BufferAccessMask.MapPersistentBit |
                    BufferAccessMask.MapCoherentBit
                );
            
            }


            /*====================================
                Chunk and Region check
            =====================================*/

            //playerChunk will be null when world first loads
            if (globalPlayerChunk == null)
            {
                globalPlayerChunk = RegionManager.GetAndLoadGlobalChunkFromCoords((int) player.GetChunkWithPlayer().GetLocation().X,//+ RegionManager.CHUNK_BOUNDS,
                     (int) player.GetChunkWithPlayer().GetLocation().Y, (int) player.GetChunkWithPlayer().GetLocation().Z);
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

          

            CountdownEvent countdown = new(chunksToRender.Count);
            foreach (KeyValuePair<string, Chunk> c in chunksToRender)
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(delegate (object state)
                {
                    //If the chunk above this one is generated, we don't need to generate this chunk   
                    if ((!c.Value.IsGenerated()) 
                    && !RegionManager.GetAndLoadGlobalChunkFromCoords(
                        (int)c.Value.xLoc, (int)(c.Value.yLoc + RegionManager.CHUNK_BOUNDS), 
                        (int)c.Value.zLoc).IsGenerated())
                    {
                        c.Value.GenerateRenderData();
                    }

                   countdown.Signal();
               }));
            }
            countdown.Wait();

            /*==========================================
             * DEPTH RENDERING PRE-PASS
             * ========================================*/
            int vaoo = GL.GenVertexArray();

            /*==========================================
             * DEPTH RENDERING PRE-PASS
             * ========================================*/
            GL.Viewport(0, 0, 4096, 4096);

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
            GL.BindVertexArray(vaoo);

            GL.DrawArraysInstanced(
                PrimitiveType.TriangleStrip,  // Drawing a triangle strip
                0,                            // Start from the first vertex in the base geometry
                4,                            // 4 vertices per face (for triangle strip)
                _nextFaceIndex                // Instance count (number of faces to draw)
            );


            //Unbind sunlight depth map at the end of frame render
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            /*==========================================
             * COLOR/TEXTURE RENDERING PASS
             * ========================================*/
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);


            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            terrainShaders?.Bind();



            /*==================================
            Render and Draw Terrain
            ====================================*/
            GL.DrawArraysInstanced(
                PrimitiveType.TriangleStrip,  // Drawing a triangle strip
                0,                            // Start from the first vertex in the base geometry
                4,                            // 4 vertices per face (for triangle strip)
                _nextFaceIndex                // Instance count (number of faces to draw)
            );

            GL.BindVertexArray(0);

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
        public static ShaderProgram GetShaders()
        {
            return terrainShaders;
        }

        public static List<Chunk> GetMenuChunks()
        {
            return menuChunks;
        }
        private static string ProcessShaderIncludes(string filePath, HashSet<string> visited = null)
        {
            visited ??= [];

            string fullPath = Path.GetFullPath(filePath);
            if (visited.Contains(fullPath))
                return ""; // Prevent circular includes

            visited.Add(fullPath);

            int index = fullPath.IndexOf("lygia", StringComparison.OrdinalIgnoreCase);
            string relativePath = index >= 0 ? fullPath.Substring(index) : fullPath;

            string[] temp = relativePath.Split('\\');

            for (int i = 0; i < temp.Length; i++)
            {
                if (i == temp.Length - 1) {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write(temp[i]);
                    Console.ResetColor();
                } else
                {
                    Console.Write(temp[i]);
                    Console.Write("/");
                }

            }
            Console.WriteLine();
            var lines = File.ReadAllLines(fullPath);
            var sb = new StringBuilder();

            foreach (var line in lines)
            {
                if (line.Trim().StartsWith("#include"))
                {
                    var match = GLSLIncludeRegex().Match(line);
                    if (match.Success)
                    {
                        string includePath = match.Groups[1].Value;
                        string resolvedPath = Path.Combine(Path.GetDirectoryName(fullPath), includePath);
                        sb.AppendLine(ProcessShaderIncludes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, resolvedPath), visited));
                    }
                }
                else
                {
                    sb.AppendLine(line);
                }
            }

            return sb.ToString();
        }

        public static IntPtr GetSSBOPtr()
        {
            return SSBOPtr;
        }
        public static int GetNextFaceIndex()
        {
            return _nextFaceIndex;
        }
        public static int GetAndIncrementNextFaceIndex()
        {
            return Interlocked.Increment(ref _nextFaceIndex) - 1;
        }

        public static void ResetSSBO()
        {
            if (SSBOPtr != IntPtr.Zero)
            {
                // Clear face data and reset index to 0
                GL.ClearNamedBufferSubData(
                    SSBOhandle,
                    PixelInternalFormat.R32ui,    // Treat as 32-bit unsigned integers
                    IntPtr.Zero,
                    SSBOSize,
                    PixelFormat.RedInteger,       // Matches how OpenGL will interpret each 4-byte component
                    PixelType.UnsignedInt,        // 4-byte units
                    IntPtr.Zero                   // Null → zero out
                );
                _nextFaceIndex = 0;
            }
        }

        public static int GetSSBOSize()
        {
            return SSBOSize;
        }
        static void Main()
        {

            Window wnd = new(GameWindowSettings.Default, new NativeWindowSettings() {
                Location = new Vector2i(0, 0),
                API = ContextAPI.OpenGL,
                Profile = ContextProfile.Core,
                Flags = ContextFlags.ForwardCompatible | ContextFlags.Debug,
                ClientSize = new Vector2i(screenWidth, screenHeight),
                APIVersion = new Version(4, 3), 
            });


            wnd.Run();
        }

        //GL Debugger delegate
        private static readonly DebugProc DebugMessageDelegate = OnDebugMessage;
        private static void OnDebugMessage(
            DebugSource source,         // Source of the debugging message.
            DebugType type,             // Type of the debugging message.
            int id,                     // ID associated with the message.
            DebugSeverity severity,     // Severity of the message.
            int length,                 // Length of the string in pMessage.
            IntPtr pMessage,            // Pointer to message string.
            IntPtr pUserParam)          // The pointer you gave to OpenGL
        {

            string message = Marshal.PtrToStringAnsi(pMessage, length);

            Console.WriteLine($"[{type}] :: Severity[{severity}] :: Source[{source}] ID[{id}] Detail: {message}");
            
            if (type == DebugType.DebugTypeError)
            {
                throw new Exception(message);
            }
        }
        
        [GeneratedRegex("#include\\s+\"([^\"]+)\"")]
        private static partial Regex GLSLIncludeRegex();
    }
}
