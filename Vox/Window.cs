using System.ComponentModel;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ImGuiNET;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Vox.Assets;
using Vox.Assets.Models;
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
using FramebufferErrorCode = OpenTK.Graphics.OpenGL4.FramebufferErrorCode;
using FramebufferTarget = OpenTK.Graphics.OpenGL4.FramebufferTarget;
using GL = OpenTK.Graphics.OpenGL4.GL;
using HintMode = OpenTK.Graphics.OpenGL4.HintMode;
using HintTarget = OpenTK.Graphics.OpenGL4.HintTarget;
using PixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat;
using PixelInternalFormat = OpenTK.Graphics.OpenGL4.PixelInternalFormat;
using PixelType = OpenTK.Graphics.OpenGL4.PixelType;
using PrimitiveType = OpenTK.Graphics.OpenGL4.PrimitiveType;
using ReadBufferMode = OpenTK.Graphics.OpenGL4.ReadBufferMode;
using RenderbufferStorage = OpenTK.Graphics.OpenGL4.RenderbufferStorage;
using RenderbufferTarget = OpenTK.Graphics.OpenGL4.RenderbufferTarget;
using StringName = OpenTK.Graphics.OpenGL4.StringName;
using TextureCompareMode = OpenTK.Graphics.OpenGL4.TextureCompareMode;
using TextureMagFilter = OpenTK.Graphics.OpenGL4.TextureMagFilter;
using TextureMinFilter = OpenTK.Graphics.OpenGL4.TextureMinFilter;
using TextureParameterName = OpenTK.Graphics.OpenGL4.TextureParameterName;
using TextureTarget = OpenTK.Graphics.OpenGL4.TextureTarget;
using TextureUnit = OpenTK.Graphics.OpenGL4.TextureUnit;
using TextureWrapMode = OpenTK.Graphics.OpenGL4.TextureWrapMode;
using Vector3 = OpenTK.Mathematics.Vector3;


namespace Vox
{
    public partial class Window(GameWindowSettings windowSettings, NativeWindowSettings nativeSettings) : GameWindow(windowSettings, nativeSettings)
    {
        ImGuiController? _UIController;
        public static readonly int screenWidth = Monitors.GetPrimaryMonitor().ClientArea.Size.X;
        public static readonly int screenHeight = Monitors.GetPrimaryMonitor().ClientArea.Size.Y - 100;

        private AssetLookup? _assetLookup;
        private TextureLoader? _textureLoader;
        private ImGuiHelper? _imguiHelper;
        private SSBOManager? _ssboManager;
        private Player? _player;
        private InventoryStore? _inventoryStore;
        private RegionManager? _regionManager;   
        private ChunkCache? _chunkCache;
        private LightHelper? _lightHelper;

        private static readonly string _assets = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\.voxelGame\\Assets\\";


        private static bool renderMenu = true;
        private static readonly string appFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\.voxelGame\\";
        public static readonly ShaderManager _shaderManager = new();
        private readonly float FOV = MathHelper.DegreesToRadians(50.0f);
        private static float angle = 0.0f;
        private static Chunk? _globalPlayerChunk = null;
        private static Region? _globalPlayerRegion = null;
        private static Matrix4 modelMatricRotate;
        private static Matrix4 viewMatrix;

        private static readonly List<Chunk> menuChunks = [];
        private static long menuSeed;
        private static Vector3 sunlightPos;
        private static int crosshairTex;
        private static int sunlightDepthMapFBO;
        private static int sunlightDepthMap;
        private static Matrix4 pMatrix;
        private Matrix4 sunlightProjectionMatrix;
        private Matrix4 sunlightViewMatrix;
        private static Matrix4 lightSpaceMatrix;
        private static int _nextFaceIndex;
        private static bool PAUSE = false;


        //used for player and lighting projection matrices
        private static float FAR = 1000.0f;
        private static float NEAR = 1f;

        private static Vector3 lightColor;
        private static Vector3 ambientColor;
        private static Vector3 diffuseColor;

        protected override void OnLoad()
        {   
            base.OnLoad();

            _assetLookup = new AssetLookup();
            _ssboManager = new SSBOManager();
            _lightHelper = new LightHelper();

            _inventoryStore = new InventoryStore(_ssboManager);
            _textureLoader = new TextureLoader(_assets, _assetLookup);
            _regionManager = new RegionManager("", _ssboManager, _lightHelper);
            _chunkCache = new ChunkCache(null, _regionManager!);
            _player = new Player(_ssboManager, _inventoryStore, _regionManager!, _chunkCache);


            _imguiHelper = new ImGuiHelper(_assetLookup, _textureLoader, _ssboManager, _player, _regionManager!, _lightHelper, _chunkCache);
            
            
            _chunkCache.SetPlayerChunk(_player.GetChunkWithPlayer());
            _chunkCache.SetRenderDistance(_regionManager.GetRenderDistance());
            sunlightPos = new(0.0f, _regionManager!.GetWorldHeight() + 100, 0.0f);


            int texArray = _textureLoader.LoadTextures(4);

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
            string vertexTerrainShaderSource = ProcessShaderIncludes("..\\..\\..\\Rendering\\Vertex\\VertexTerrainShader.glsl");
            string fragmentTerrainShaderSource = ProcessShaderIncludes("..\\..\\..\\Rendering\\Fragment\\FragTerrainShader.glsl");


            _shaderManager.AddShaderProgram("Terrain", new())
                .CreateVertexShader("VertexTerrainShader", vertexTerrainShaderSource)
                .CreateFragmentShader("FragTerrainShader", fragmentTerrainShaderSource)
                .Link();
            //-----------------------Terrain shaders---------------------------------
            visited.Clear();
            //-----------------------Lighting shaders---------------------------------
            string vertexLightingShaderSource = ShaderProgram.LoadShaderFromFile("..\\..\\..\\Rendering\\Vertex\\VertexDepthShader.glsl");
            string fragmentLightingSource = ShaderProgram.LoadShaderFromFile("..\\..\\..\\Rendering\\Fragment\\FragDepthShader.glsl");

            _shaderManager.AddShaderProgram("Lighting", new())
                .CreateVertexShader("VertexDepthShader", vertexLightingShaderSource)
                .CreateFragmentShader("FragDepthShader", fragmentLightingSource)
                .Link();
            //-----------------------Lighting shaders---------------------------------

            //-----------------------Lighting shaders---------------------------------     
            string vertexCrosshairSource = ShaderProgram.LoadShaderFromFile("..\\..\\..\\Rendering\\Vertex\\Vertex_Crosshair.glsl");
            string fragmentCrosshairSource = ShaderProgram.LoadShaderFromFile("..\\..\\..\\Rendering\\Fragment\\Frag_Crosshair.glsl");

            _shaderManager.AddShaderProgram("Crosshair", new())
                .CreateVertexShader("Vertex_Crosshair", vertexCrosshairSource)  
                .CreateFragmentShader("Frag_Crosshair", fragmentCrosshairSource)
                .Link();
            //-----------------------Lighting shaders---------------------------------

            //------------------------Inventory shaders---------------------------------
            string vertexInventorySource = ProcessShaderIncludes("..\\..\\..\\Rendering\\Vertex\\VertexInventoryShader.glsl");
            string fragInventorySource = ProcessShaderIncludes("..\\..\\..\\Rendering\\Fragment\\FragInventoryShader.glsl");
            _shaderManager.AddShaderProgram("Inventory", new())
                .CreateVertexShader("VertexInventoryShader", vertexInventorySource)
                .CreateFragmentShader("FragInventoryShader", fragInventorySource)
                .Link();
            //------------------------Inventory shaders---------------------------------

            //Sunlight frame buffer for shadow map
            sunlightDepthMapFBO = GL.GenFramebuffer();
            sunlightDepthMap = GL.GenTexture();

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, sunlightDepthMap);
            GL.TexImage2D(
                TextureTarget.Texture2D, 
                0, 
                PixelInternalFormat.DepthComponent32,
                4096, 4096, 
                0, 
                PixelFormat.DepthComponent, 
                PixelType.Float, 
                IntPtr.Zero
            );
            GL.Viewport(0, 0, 4096, 4096);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareFunc, (int)DepthFunction.Less);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareMode, (int)TextureCompareMode.CompareRefToTexture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            //Attach depth texture to frame buffer         
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, sunlightDepthMapFBO);
            GL.FramebufferTexture2D(
                FramebufferTarget.Framebuffer, 
                FramebufferAttachment.DepthAttachment,
                TextureTarget.Texture2D, 
                sunlightDepthMap, 
                0
            );
            GL.DrawBuffer(DrawBufferMode.None);
            GL.ReadBuffer(ReadBufferMode.None);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);


            //------------------------Inventory FrameBuffer---------------------------------
            //Inventory animation framebuffer
            ImGuiHelper._inventoryIconFBO = GL.GenFramebuffer();
            ImGuiHelper._inventoryIconTexture = GL.GenTexture();

            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, ImGuiHelper._inventoryIconTexture);
            GL.TexImage2D(
                TextureTarget.Texture2D,
                0,
                PixelInternalFormat.Rgba,
                256, 256,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                IntPtr.Zero
            );
            GL.Viewport(0, 0, 256, 256);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            //Attach color texture to frame buffer
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, ImGuiHelper._inventoryIconFBO);
            GL.FramebufferTexture2D(
                FramebufferTarget.Framebuffer,
                FramebufferAttachment.ColorAttachment0,
                TextureTarget.Texture2D,
                ImGuiHelper._inventoryIconTexture,
                0
            );

            // Depth renderbuffer for the 3D inventory FBO
            int inventoryDepthRBO = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, inventoryDepthRBO);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent24, 256, 256);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, inventoryDepthRBO);
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);

            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);


            //========================
            //Matrix unifrom setup
            //========================

            pMatrix = Matrix4.CreatePerspectiveFieldOfView(FOV, (float)ClientSize.X / ClientSize.Y, NEAR, FAR);

            viewMatrix = Matrix4.LookAt(new Vector3(-10f, 220f, -20f), new Vector3(8f, 200f, 8f), new Vector3(0.0f, 1f, 0.0f));

            float dist = _regionManager.GetChunkBounds() * _regionManager.GetRenderDistance();
            sunlightProjectionMatrix = Matrix4.CreateOrthographicOffCenter(-dist, dist, -dist, dist, 1f, dist * 2);

            _shaderManager.GetShaderProgram("Lighting").Bind()
                .CreateUniform("lightModel")
                .CreateUniform("lightViewMatrix")
                .CreateUniform("lightProjMatrix");

            _shaderManager.GetShaderProgram("Terrain").Bind()
                .CreateUniform("texture_sampler")
                .CreateUniform("sunlightDepth_sampler")
                .CreateUniform("chunkSize")
                .CreateUniform("crosshairOrtho")
                .CreateUniform("targetTexLayer")
                .CreateUniform("lightSpaceMatrix")
                .CreateUniform("blockCenter")
                .CreateUniform("curPos")
                .CreateUniform("localHit")
                .CreateUniform("renderDistance")
                .CreateUniform("playerPos")
                .CreateUniform("forwardDir")
                .CreateUniform("targetVertex")
                .CreateUniform("projectionMatrix")
                .CreateUniform("modelMatrix")
                .CreateUniform("viewMatrix")
                .CreateUniform("chunkModelMatrix")
                .CreateUniform("isMenuRendered")

                .UploadAndBindTexture("texture_sampler", 3, texArray, (OpenTK.Graphics.OpenGL.TextureTarget)TextureTarget.Texture2DArray)
                .UploadAndBindTexture("sunlightDepth_sampler", 1, sunlightDepthMap, (OpenTK.Graphics.OpenGL.TextureTarget)TextureTarget.Texture2D)
              //  .UploadAndBindTexture("inventory_texture_sampler", 1, sunlightDepthMap, (OpenTK.Graphics.OpenGL.TextureTarget)TextureTarget.Texture2D)
                .SetIntFloatUniform("chunkSize", _regionManager.GetChunkBounds())               
                .SetMatrixUniform("crosshairOrtho", Matrix4.CreateOrthographic(screenWidth, screenHeight, 0.1f, 10f));

            _shaderManager.GetShaderProgram("Inventory").Bind()
                .UploadAndBindTexture("texture_sampler", 3, texArray, (OpenTK.Graphics.OpenGL.TextureTarget)TextureTarget.Texture2DArray)
                .CreateUniform("viewMatrix")
                .CreateUniform("modelMatrix")
                .CreateUniform("projectionMatrix")
                .CreateUniform("chunkModelMatrix")
                .CreateUniform("lightProjMatrix")
                .CreateUniform("lightModel")
                .CreateUniform("lightViewMatrix")
                .CreateUniform("playerPos")
                .CreateUniform("forwardDir")
                .CreateUniform("playerMin")
                .CreateUniform("playerMax");
            
                


            // This is where we change the lights color over time using the sin function
            float time = DateTime.Now.Second + DateTime.Now.Millisecond / 1000f;
            lightColor.X = 1.4f; //(MathF.Sin(time * 2.0f) + 1) / 2f;
            lightColor.Y = 1f;//(MathF.Sin(time * 0.7f) + 1) / 2f;
            lightColor.Z = 1f; //(MathF.Sin(time * 1.3f) + 1) / 2f;


            // The ambient light is less intensive than the diffuse light in order to make it less dominant
            ambientColor = lightColor * new Vector3(0.2f);
            diffuseColor = lightColor * new Vector3(0.5f);


            /*======================================
            Block face Terrain SSBO instancing setup
            =======================================*/

            // SSBO Size based on nrender distance
            int blockFacesPerBlock = 6;
            int maxBlocksPerChunk = (int)Math.Pow(_regionManager.GetChunkBounds(), 3);
            int maxChunksInCache = (int)Math.Pow(_regionManager.GetRenderDistance(), 3);
            int TerrainSSBOSize =((maxBlocksPerChunk * maxChunksInCache * blockFacesPerBlock) * Marshal.SizeOf<BlockFaceInstance>());

            _ssboManager.AddSSBO(TerrainSSBOSize, 0, SSBO.Terrain);

            /*===================================
            Inventory animation model SSBO
            ====================================*/
            _ssboManager.AddSSBO(Marshal.SizeOf<BlockFaceInstance>() * 6, 1, SSBO.Inventory);

            /*===================================
            Render menu screen
            ====================================*/
            menuChunks.Add(_regionManager.GetAndLoadGlobalChunkFromCoords(0, 176, 0));

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


            GL.ClearColor(1.5f, 0.8f, 1.0f, 0.0f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);

            if (renderMenu == true)
            {
                if (angle > 360)
                    angle = 0.0f;

                angle += 0.12f * (float) e.Time;

                _shaderManager.GetShaderProgram("Terrain").Bind()
                    .SetIntFloatUniform("isMenuRendered", 1);

                RenderMenu();
            }
            else
            {
               
                _shaderManager.GetShaderProgram("Terrain").Bind()
                    .SetIntFloatUniform("isMenuRendered", 0);

                RenderWorld();
            }

            //Render inventory animation
            //Angle variable now used by inventory instead of main menu
            if (_imguiHelper!.ShowPlayerInventory())
            {
                if (angle > 360)
                    angle = 0.0f;

                angle += 0.1f * (float)e.Time;
                _shaderManager.GetShaderProgram("Inventory").Bind();
            }

            ImGuiController.CheckGLError("End of frame");

            /*======================================
            Render new UI frame over everything
            ========================================*/
            RenderUI();
            _UIController.Render();

            SwapBuffers();

        }

        public void SetTerrainShaderUniforms()
        {
            _shaderManager.GetShaderProgram("Terrain").Bind()
                .SetMatrixUniform("projectionMatrix", pMatrix)
                .SetMatrixUniform("modelMatrix", modelMatricRotate);

            if (IsMenuRendered())
            {
                modelMatricRotate = Matrix4.CreateFromAxisAngle(new Vector3(100f, 2000, 100f), angle);
                Matrix4 menuMatrix = modelMatricRotate;
                _shaderManager.GetShaderProgram("Terrain").Bind()
                    .SetMatrixUniform("modelMatrix", menuMatrix)
                    .SetMatrixUniform("viewMatrix", viewMatrix);
            }
            else
            {
                _shaderManager.GetShaderProgram("Terrain").Bind()
                    .SetIntFloatUniform("targetTexLayer", _assetLookup!.GetTextureValueFromFilename("target.png"))
                    .SetVector3Uniform("targetVertex", GetPlayer().UpdateViewTarget(out _, out _, out _))
                    .SetVector3Uniform("playerMin", GetPlayer().GetBoundingBox()[0])
                    .SetVector3Uniform("playerMax", GetPlayer().GetBoundingBox()[1])
                    .SetMatrixUniform("viewMatrix", GetPlayer().GetViewMatrix());


                _shaderManager.GetShaderProgram("Inventory").Bind()
                    .SetMatrixUniform("projectionMatrix", _inventoryStore!.GetIconProjection())
                    .SetMatrixUniform("viewMatrix", _inventoryStore.GetIconViewMatrix())
                    .SetMatrixUniform("modelMatrix", _inventoryStore.GetIconModelMatrix())
                    .SetVector3Uniform("playerMin", GetPlayer().GetBoundingBox()[0])
                    .SetVector3Uniform("playerMax", GetPlayer().GetBoundingBox()[1]);

            }

            _shaderManager.GetShaderProgram("Terrain").Bind()
                .SetVector3Uniform("forwardDir", GetPlayer().GetForwardDirection())
                .SetVector3Uniform("playerPos", GetPlayer().GetPosition())
                .SetIntFloatUniform("renderDistance", _regionManager!.GetRenderDistance())
                .SetMatrixUniform("chunkModelMatrix", Chunk.GetModelMatrix())
                .SetIntFloatUniform("isMenuRendered", 1);

            _shaderManager.GetShaderProgram("Inventory").Bind()
                .SetVector3Uniform("forwardDir", GetPlayer().GetForwardDirection())
                .SetVector3Uniform("playerPos", GetPlayer().GetPosition());
        }



        //Track cursor position on color picker open
        private static float dirX = 0.0f;
        private static float dirY = 0.0f;
        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);

            //Update player and cursor state
            if (!IsMenuRendered())
            {
                GetPlayer().Update((float)args.Time);

                if (!_imguiHelper!.ShowBlockColorPicker() && !PAUSE && !_imguiHelper!.ShowPlayerInventory())
                    _player!.SetLookDir(MouseState.Y, MouseState.X);
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

            _shaderManager.GetShaderProgram("Terrain").Bind()
                .SetVector3Uniform("light.position", sunlightPos)
                .SetVector3Uniform("light.ambient", ambientColor)
                .SetVector3Uniform("light.diffuse", diffuseColor)
                .SetVector3Uniform("light.specular", new Vector3(1.0f, 1.0f, 1.0f));



            //Move light in a circlular path around the player to simulate sunlight
            //x = h + r⋅cos(θ)
            //y = k + r⋅sin(θ)
            float radius = _regionManager!.GetChunkBounds() * _regionManager.GetRenderDistance() * 2f;
            Vector3 playerPos = GetPlayer().GetPosition();
            float x = playerPos.X + radius * MathF.Cos(angle);
            float z = playerPos.Z + radius * MathF.Sin(angle);
            float y = playerPos.Y + radius * 0.5f * MathF.Sin(angle);

            sunlightPos = new Vector3(x, y, z);



            SetTerrainShaderUniforms();

            _shaderManager.GetShaderProgram("Lighting").Bind()
                .SetVector3Uniform("light.position", sunlightPos);

            if (!IsMenuRendered())
                sunlightViewMatrix = Matrix4.LookAt(sunlightPos, new(0, 0, 0), Vector3.UnitY);
            else
                sunlightViewMatrix = Matrix4.LookAt(sunlightPos, GetPlayer().GetPosition(), Vector3.UnitY);

            float dist1 = _regionManager.GetChunkBounds() * _regionManager.GetRenderDistance();
            sunlightProjectionMatrix = Matrix4.CreateOrthographicOffCenter(-dist1, dist1, -dist1, dist1 , 1f, dist1);

            lightSpaceMatrix = sunlightViewMatrix * sunlightProjectionMatrix;


            _shaderManager.GetShaderProgram("Lighting").Bind()
                .SetMatrixUniform("lightViewMatrix", sunlightViewMatrix)
                .SetMatrixUniform("lightProjMatrix", sunlightProjectionMatrix)
                .SetMatrixUniform("lightModel", Chunk.GetModelMatrix());

            _shaderManager.GetShaderProgram("Terrain").Bind()
                .SetMatrixUniform("lightViewMatrix", sunlightViewMatrix)
                .SetMatrixUniform("lightProjMatrix", sunlightProjectionMatrix)
                .SetMatrixUniform("lightModel", Chunk.GetModelMatrix());

            _shaderManager.GetShaderProgram("Inventory").Bind()
                .SetMatrixUniform("lightViewMatrix", sunlightViewMatrix)
                .SetMatrixUniform("lightProjMatrix", sunlightProjectionMatrix)
                .SetMatrixUniform("lightModel", Chunk.GetModelMatrix());
        }

        private KeyboardState previousKeyboardState;
        protected override void OnKeyDown(KeyboardKeyEventArgs e)
        {
            base.OnKeyDown(e);
            KeyboardState current = KeyboardState.GetSnapshot();

            if (float.IsNaN(GetPlayer().GetBlockedDirection().X))
                return;

            if (!IsMenuRendered() && !_imguiHelper!.ShowBlockColorPicker()) {
                if (current[Keys.W])// && Math.Sign(GetPlayer().GetForwardDirection().Z) != Math.Sign(GetPlayer().GetBlockedDirection().Z))
                    GetPlayer().MoveForward(1);
                if (current[Keys.S] && Math.Sign(-GetPlayer().GetForwardDirection().Z) != Math.Sign(GetPlayer().GetBlockedDirection().Z)) GetPlayer().MoveForward(-1);
                if (current[Keys.A] && Math.Sign(-GetPlayer().GetRightDirection().X) != Math.Sign(GetPlayer().GetBlockedDirection().X)) GetPlayer().MoveRight(-1);
                if (current[Keys.D] && Math.Sign(GetPlayer().GetRightDirection().X) != Math.Sign(GetPlayer().GetBlockedDirection().X)) GetPlayer().MoveRight(1);
                               
                if (current[Keys.Space] && Math.Sign(GetPlayer().GetBlockedDirection().Y) != 1)
                    GetPlayer().MoveUp(3);

                if (current[Keys.LeftShift] && Math.Sign(GetPlayer().GetBlockedDirection().Y) != -1)
                    GetPlayer().MoveUp(-1);

                if (current[Keys.Escape])
                    PAUSE = !PAUSE;

            }

            Vector3 target = new(0, 0, 0);
            if (!_imguiHelper!.ShowPlayerInventory())
                target = GetPlayer().UpdateViewTarget(out _, out _, out Vector3 blockSpace);

            Chunk actionChunk = _regionManager!.GetAndLoadGlobalChunkFromCoords(target);
            Vector3i idx = _regionManager.GetChunkRelativeCoordinates(target);

            //If its a lamp block, display the color picker when the key is pressed to display it
            if (current[Keys.C] && (BlockType)actionChunk._blockData[idx.X, idx.Y, idx.Z] == BlockType.LAMP_BLOCK)
            {
                if (!_imguiHelper!.ShowBlockColorPicker())
                {
                    _imguiHelper.SetShowBlockColorPicker(true);
                    dirX = MouseState.X;
                    dirY = MouseState.Y;
                } else
                {
                    _imguiHelper.SetShowBlockColorPicker(false);
                    Console.WriteLine("Set look direction to X: " + dirX + " Y: " + dirY);
                    _player!.SetLookDir(dirX, dirY);
                }
            }

            // Show player inventory
            if (current[Keys.E] && !IsMenuRendered())
            {
                _imguiHelper.SetShowPlayerInventory(!_imguiHelper.ShowPlayerInventory());

                // If both inventory and color picker is showing, close the color picker
                if (_imguiHelper.ShowPlayerInventory() && _imguiHelper.ShowBlockColorPicker())
                    _imguiHelper.SetShowBlockColorPicker(false);
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

            if (!IsMenuRendered() && !_imguiHelper!.ShowPlayerInventory())
            {
                Vector3 block = GetPlayer().UpdateViewTarget(out BlockFace playerFacing, out Vector3 blockFace, out Vector3 blockSpace);

                if (e.Button == MouseButton.Left && !_imguiHelper!.IsAnyMenuActive())
                {
                    ColorVector color = new(9, 9, 8);
                    _regionManager!.AddBlockToChunk(blockSpace, GetPlayer().GetPlayerBlockType(), color);

                }
                if (e.Button == MouseButton.Right && !_imguiHelper!.IsAnyMenuActive())
                {
                    _regionManager!.RemoveBlockFromChunk(block);
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
                _imguiHelper!.ShowWorldMenu(ioptr);
            }
            else
            {
                  _imguiHelper!.ShowDebugMenu(ioptr);
            }

            //Color Picker
            KeyboardState current = KeyboardState.GetSnapshot();
            Vector3 target = (0, 0, 0);
            
            if (!_imguiHelper!.ShowPlayerInventory())
                target = GetPlayer().UpdateViewTarget(out _, out _, out _);

            //Cursor state handling
            if (_imguiHelper!.ShowPlayerInventory() && !IsMenuRendered())
            {
                CursorState = CursorState.Normal;
                _imguiHelper!.CreatePlayerInventory(_UIController);
            }
            else if (_imguiHelper!.ShowBlockColorPicker() && !IsMenuRendered())
            {
                CursorState = CursorState.Normal;
                _imguiHelper.SetShowBlockColorPicker(true);
                _imguiHelper!.CreateBlockColorPicker(target);
                ImGui.OpenPopup("ColorPicker");
            }
            else if (!_imguiHelper!.ShowBlockColorPicker() && !IsMenuRendered() && !PAUSE)
            {
                ImGui.CloseCurrentPopup();
                CursorState = CursorState.Grabbed;
            } else if (IsMenuRendered() || PAUSE)
            {
                CursorState = CursorState.Normal;
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
            _shaderManager.GetShaderProgram("Lighting").Bind();
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

            _shaderManager.GetShaderProgram("Terrain").Bind();



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

        public RegionManager? GetLoadedWorld()
        {
            return (RegionManager?) _regionManager;
        }
        public void SetLoadedWorld(RegionManager rm)
        {
            _regionManager = rm;
        }
        public static string GetAppFolder()
        {
            return appFolder;   
        }
        private void RenderWorld()
        {
            //Remeber to clear cache each frame 
            _chunkCache!.ClearChunkCache();

            //Recalculate SSBO size
            int blockFacesPerBlock = 6;
            int maxBlocksPerChunk = (int)Math.Pow(_regionManager!.GetChunkBounds(), 3);
            int maxChunksInCache = (int)Math.Pow(_regionManager.GetRenderDistance(), 3);

            //If SSBO size changed (i.e render distance was increased or decreased) 
            if (Marshal.SizeOf<BlockFaceInstance>() * (maxBlocksPerChunk * maxChunksInCache * blockFacesPerBlock) != _ssboManager!.GetSSBO(SSBO.Terrain).Size)
            {
                Console.WriteLine("Size Change");

                int SSBOResize = Marshal.SizeOf<BlockFaceInstance>() * (maxBlocksPerChunk * maxChunksInCache * blockFacesPerBlock);
                _ssboManager!.AddSSBO(SSBOResize, 0, SSBO.Terrain);
                //Creates ssbo buffer
                // GL.BufferStorage(BufferTarget.ShaderStorageBuffer, SSBOSize, IntPtr.Zero, BufferStorageFlags.MapPersistentBit | BufferStorageFlags.MapCoherentBit | BufferStorageFlags.MapWriteBit);
                //
                // //Map binding for shader to use
                // GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, SSBOhandle);
                //
                // //Creates pointer to SSBO buffer
                // SSBOPtr = GL.MapBufferRange(
                //     BufferTarget.ShaderStorageBuffer,
                //     IntPtr.Zero,
                //     SSBOSize,
                //     BufferAccessMask.MapWriteBit |
                //     BufferAccessMask.MapPersistentBit |
                //     BufferAccessMask.MapCoherentBit
                // );

            }


            /*====================================
                Chunk and Region check
            =====================================*/

            //playerChunk will be null when world first loads
            if (_globalPlayerChunk == null)
            {
                _globalPlayerChunk = _regionManager.GetAndLoadGlobalChunkFromCoords((int) _player!.GetChunkWithPlayer().GetLocation().X,//+ _regionManager.GetChunkBounds(),
                     (int) _player!.GetChunkWithPlayer().GetLocation().Y, (int) _player.GetChunkWithPlayer().GetLocation().Z);
                _player.SetPosition(new(_player.GetPosition().X, _player.GetPosition().Y + 10, _player.GetPosition().Z));
            }

            //Updates the chunks to render when the player has moved into a new chunk
            _chunkCache.SetPlayerChunk(_globalPlayerChunk);
            Dictionary<string, Chunk> chunksToRender = _chunkCache!.UpdateChunkCache();


            if (!_player!.GetChunkWithPlayer().Equals(_globalPlayerChunk))
            {
                _chunkCache.UpdateVisibleRegions();
                _globalPlayerChunk = _player.GetChunkWithPlayer();
                _chunkCache.SetPlayerChunk(_globalPlayerChunk);

                chunksToRender = _chunkCache.UpdateChunkCache();       

                //Updates the regions when player moves into different region
                if (!_player.GetRegionWithPlayer().Equals(_globalPlayerRegion))
                    _globalPlayerRegion = _player.GetRegionWithPlayer();
            }
            //=========================================================================

          

            CountdownEvent countdown = new(chunksToRender.Count);
            foreach (KeyValuePair<string, Chunk> c in chunksToRender)
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(delegate (object state)
                {
                    //If the chunk above this one is generated, we don't need to generate this chunk   
                    if ((!c.Value.IsGenerated()) 
                    && !_regionManager.GetAndLoadGlobalChunkFromCoords(
                        (int)c.Value.xLoc, (int)(c.Value.yLoc + _regionManager.GetChunkBounds()), 
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
            _shaderManager.GetShaderProgram("Lighting").Bind();
            GL.BindVertexArray(vaoo);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _ssboManager.GetSSBO(SSBO.Terrain).Handle);
            GL.BindBufferBase(OpenTK.Graphics.OpenGL4.BufferRangeTarget.ShaderStorageBuffer, 0, _ssboManager.GetSSBO(SSBO.Terrain).Handle);

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

            _shaderManager.GetShaderProgram("Terrain").Bind();


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
            _shaderManager.CleanupShaders();
            //TextureLoader.Unbind();
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);

            //Update ortho matrix for crosshair on window resize
            _shaderManager.GetShaderProgram("Terrain").SetMatrixUniform("crosshairOrtho", Matrix4.CreateOrthographic(screenWidth, screenHeight, 0.1f, 10f));

            // Update the opengl viewport
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);

            Matrix4 pMatrix = Matrix4.CreatePerspectiveFieldOfView(FOV, (float)e.Width / e.Height, NEAR, FAR);
            _shaderManager.GetShaderProgram("Terrain").Bind().SetMatrixUniform("projectionMatrix", pMatrix);

            // Tell ImGui of the new size
            _UIController.WindowResized(ClientSize.X, ClientSize.Y);
        }

        public Player GetPlayer() {
            _player ??= new Player(_ssboManager!, _inventoryStore!, _regionManager!, _chunkCache!);

            return (Player) _player;
        }

        public static List<Chunk> GetMenuChunks()
        {
            return menuChunks;
        }
        private static HashSet<string> visited = new();

        private static string ProcessShaderIncludes(string filePath)
        {
            string fullPath = Path.GetFullPath(filePath);

            if (visited.Contains(fullPath))
                return ""; // Prevent circular includes

            visited.Add(fullPath);

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
                        string resolvedPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fullPath)!, includePath));
                        sb.AppendLine(ProcessShaderIncludes(resolvedPath));
                        
                    }
                    else 
                        throw new Exception($"Invalid include directive: {line}"); 
                }
                else
                    sb.AppendLine(line);
            }

            return sb.ToString();
        }


        public static int GetAndIncrementNextFaceIndex()
        {
            return Interlocked.Increment(ref _nextFaceIndex) - 1;
        }

        public static float GetAngle() { 
            return angle;
        }
        public static string GetAssetPath()
        {
            return _assets;
        }

        public static void IncrementAngle(float inc) { 
            angle += inc;
        }
        static void Main()
        {

            Window wnd = new(GameWindowSettings.Default, new NativeWindowSettings() {
                Location = new Vector2i(0, 0),
                API = ContextAPI.OpenGL,
                Profile = ContextProfile.Core,
                Flags = ContextFlags.ForwardCompatible | ContextFlags.Debug,
                ClientSize = new Vector2i(screenWidth, screenHeight),
                APIVersion = new Version(4, 6), 
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
