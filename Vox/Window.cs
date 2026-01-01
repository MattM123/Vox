using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ImGuiNET;
using OpenTK.Graphics.OpenGL;
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
        ImGuiController _UIController;
        public static readonly int screenWidth = Monitors.GetPrimaryMonitor().ClientArea.Size.X;
        public static readonly int screenHeight = Monitors.GetPrimaryMonitor().ClientArea.Size.Y - 100;

        private static bool renderMenu = true;
        private static readonly string appFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\.voxelGame\\";
        public static readonly ShaderManager shaderManager = new();
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
        private static readonly List<Chunk> menuChunks = [];
        private static long menuSeed;
        private static Vector3 _lightPos = new(0.0f, RegionManager.CHUNK_HEIGHT + 100, 0.0f);
        private static int crosshairTex;
        private static int sunlightDepthMapFBO;
        private static int sunlightDepthMap;
        public static int inventoryAnimFBO;
        public static int inventoryAnim;
        private static Matrix4 pMatrix;
        private Matrix4 sunlightProjectionMatrix;
        private Matrix4 sunlightViewMatrix;
        private static Matrix4 lightSpaceMatrix;
        private static int _nextFaceIndex;
        private static bool PAUSE = false;
        public static SSBOManager ssboManager = new();

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


            shaderManager.AddShaderProgram("Terrain", new())
                .CreateVertexShader("VertexTerrainShader", vertexTerrainShaderSource)
                .CreateFragmentShader("FragTerrainShader", fragmentTerrainShaderSource)
                .Link();
            //-----------------------Terrain shaders---------------------------------
            visited.Clear();
            //-----------------------Lighting shaders---------------------------------
            string vertexLightingShaderSource = ShaderProgram.LoadShaderFromFile("..\\..\\..\\Rendering\\Vertex\\VertexDepthShader.glsl");
            string fragmentLightingSource = ShaderProgram.LoadShaderFromFile("..\\..\\..\\Rendering\\Fragment\\FragDepthShader.glsl");

            shaderManager.AddShaderProgram("Lighting", new())
                .CreateVertexShader("VertexDepthShader", vertexLightingShaderSource)
                .CreateFragmentShader("FragDepthShader", fragmentLightingSource)
                .Link();
            //-----------------------Lighting shaders---------------------------------

            //-----------------------Lighting shaders---------------------------------     
            string vertexCrosshairSource = ShaderProgram.LoadShaderFromFile("..\\..\\..\\Rendering\\Vertex\\Vertex_Crosshair.glsl");
            string fragmentCrosshairSource = ShaderProgram.LoadShaderFromFile("..\\..\\..\\Rendering\\Fragment\\Frag_Crosshair.glsl");

            shaderManager.AddShaderProgram("Crosshair", new())
                .CreateVertexShader("Vertex_Crosshair", vertexCrosshairSource)  
                .CreateFragmentShader("Frag_Crosshair", fragmentCrosshairSource)
                .Link();
            //-----------------------Lighting shaders---------------------------------

            //------------------------Inventory shaders---------------------------------
            string vertexInventorySource = ShaderProgram.LoadShaderFromFile("..\\..\\..\\Rendering\\Vertex\\VertexInventoryShader.glsl");
            shaderManager.AddShaderProgram("Inventory", new())
                .CreateVertexShader("VertexInventoryShader", vertexInventorySource)
                .CreateFragmentShader("FragTerrainShader", fragmentTerrainShaderSource)
                .Link();
            Console.WriteLine(vertexInventorySource);
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
            inventoryAnimFBO = GL.GenFramebuffer();
            inventoryAnim = GL.GenTexture();

            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, inventoryAnim);
            GL.TexImage2D(
                TextureTarget.Texture2D, 
                0, 
                PixelInternalFormat.Rgba8,
                 4096, 4096, 
                 0, 
                 PixelFormat.Rgba, 
                 PixelType.UnsignedByte, 
                 IntPtr.Zero
            );
            GL.Viewport(0, 0, 4096, 4096);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            //Attach depth texture to frame buffer         
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, inventoryAnimFBO);
            GL.FramebufferTexture2D(
                FramebufferTarget.Framebuffer,
                FramebufferAttachment.ColorAttachment0, 
                TextureTarget.Texture2D, 
                inventoryAnim, 
                0
            );
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);



            //========================
            //Matrix unifrom setup
            //========================

            pMatrix = Matrix4.CreatePerspectiveFieldOfView(FOV, (float)ClientSize.X / ClientSize.Y, NEAR, FAR);

            viewMatrix = Matrix4.LookAt(new Vector3(-10f, 220f, -20f), new Vector3(8f, 200f, 8f), new Vector3(0.0f, 1f, 0.0f));

            float dist = RegionManager.CHUNK_BOUNDS * RegionManager.GetRenderDistance();
            sunlightProjectionMatrix = Matrix4.CreateOrthographicOffCenter(-dist, dist, -dist, dist, 1f, dist * 2);

            shaderManager.GetShaderProgram("Lighting").Bind();

            shaderManager.GetShaderProgram("Lighting")
                .CreateUniform("lightModel")
                .CreateUniform("lightViewMatrix")
                .CreateUniform("lightProjMatrix");

            shaderManager.GetShaderProgram("Terrain").Bind();

            shaderManager.GetShaderProgram("Terrain")
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
                .CreateUniform("isMenuRendered");

            shaderManager.GetShaderProgram("Terrain")
                .UploadAndBindTexture("texture_sampler", 0, texArray, (OpenTK.Graphics.OpenGL.TextureTarget)TextureTarget.Texture2DArray)
                .UploadAndBindTexture("sunlightDepth_sampler", 1, sunlightDepthMap, (OpenTK.Graphics.OpenGL.TextureTarget)TextureTarget.Texture2D)
                .UploadAndBindTexture("inventory_texture_sampler", 1, sunlightDepthMap, (OpenTK.Graphics.OpenGL.TextureTarget)TextureTarget.Texture2D)

                .SetIntFloatUniform("chunkSize", RegionManager.CHUNK_BOUNDS)               
                .SetMatrixUniform("crosshairOrtho", Matrix4.CreateOrthographic(screenWidth, screenHeight, 0.1f, 10f));

            shaderManager.GetShaderProgram("Inventory")
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
            Block face SSBO instancing setup
            =======================================*/

            // SSBO Size based on nrender distance
            int blockFacesPerBlock = 6;
            int maxBlocksPerChunk = (int)Math.Pow(RegionManager.CHUNK_BOUNDS, 3);
            int maxChunksInCache = (int)Math.Pow(RegionManager.GetRenderDistance(), 3);
            int TerrainSSBOSize =((maxBlocksPerChunk * maxChunksInCache * blockFacesPerBlock) * Marshal.SizeOf<BlockFaceInstance>());

            ssboManager.AddSSBO(TerrainSSBOSize, 0, "Terrain" );

            /*===================================
            Inventory animation model SSBO
            ====================================*/
            ssboManager.AddSSBO(Marshal.SizeOf<BlockFaceInstance>() * 6, 1, "Inventory");

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

            if (loadedWorld == null)
            {
                if (angle > 360)
                    angle = 0.0f;

                angle += 0.1f * (float) e.Time;
                renderMenu = true;

                shaderManager.GetShaderProgram("Terrain")
                    .Bind()
                    .SetIntFloatUniform("isMenuRendered", 1);

                RenderMenu();
            }
            else
            {
                
                renderMenu = false;

                shaderManager.GetShaderProgram("Terrain")
                    .Bind()
                    .SetIntFloatUniform("isMenuRendered", 0);

                RenderWorld();
            }

            //Render inventory animation
            //Angle variable now used by inventory instead of main menu
            if (ImGuiHelper.SHOW_PLAYER_INVENTORY)
            {
                if (angle > 360)
                    angle = 0.0f;

                angle += 0.1f * (float)e.Time;
                // renderMenu = true;
                shaderManager.GetShaderProgram("Inventory").Bind();
               // terrainShaders?.SetIntFloatUniform("isMenuRendered", 1);

                //RenderInventoryAnimation();
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
            shaderManager.GetShaderProgram("Terrain")
                .Bind()
                .SetMatrixUniform("projectionMatrix", pMatrix)
                .SetMatrixUniform("modelMatrix", modelMatricRotate);

            if (IsMenuRendered())
            {
                modelMatricRotate = Matrix4.CreateFromAxisAngle(new Vector3(100f, 2000, 100f), angle);
                Matrix4 menuMatrix = modelMatricRotate;
                shaderManager.GetShaderProgram("Terrain")
                    .SetMatrixUniform("modelMatrix", menuMatrix)
                    .SetMatrixUniform("viewMatrix", viewMatrix);
            }
            else
            {
                shaderManager.GetShaderProgram("Terrain")
                    .SetIntFloatUniform("targetTexLayer", AssetLookup.GetTextureValue("target.png"))
                    .SetVector3Uniform("targetVertex", GetPlayer().UpdateViewTarget(out _, out _, out _))
                    .SetVector3Uniform("playerMin", GetPlayer().GetBoundingBox()[0])
                    .SetVector3Uniform("playerMax", GetPlayer().GetBoundingBox()[1])
                    .SetMatrixUniform("viewMatrix", GetPlayer().GetViewMatrix())
                    .SetVector3Uniform("playerMin", Player.playerMin)
                    .SetVector3Uniform("playerMax", Player.playerMax);

                shaderManager.GetShaderProgram("Inventory")
                    .SetMatrixUniform("projectionMatrix", pMatrix)
                    .SetMatrixUniform("viewMatrix", GetPlayer().GetViewMatrix())
                    .SetMatrixUniform("modelMatrix", modelMatricRotate)
                    .SetMatrixUniform("chunkModelMatrix", Chunk.GetModelMatrix())
                    .SetVector3Uniform("playerMin", Player.playerMin)
                    .SetVector3Uniform("playerMax", Player.playerMax);

            }

            shaderManager.GetShaderProgram("Terrain")
                .SetVector3Uniform("forwardDir", GetPlayer().GetForwardDirection())
                .SetVector3Uniform("playerPos", GetPlayer().GetPosition())
                .SetIntFloatUniform("renderDistance", RegionManager.GetRenderDistance())
                .SetMatrixUniform("chunkModelMatrix", Chunk.GetModelMatrix())
                .SetIntFloatUniform("isMenuRendered", 1);
              // .SetVector3Uniform("playerMin", GetPlayer().GetBoundingBox()[0])
               // .SetVector3Uniform("playerMax", GetPlayer().GetBoundingBox()[1]);    
            
            shaderManager.GetShaderProgram("Inventory")
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

                if (!ImGuiHelper.SHOW_BLOCK_COLOR_PICKER && !PAUSE && !ImGuiHelper.SHOW_PLAYER_INVENTORY)
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
            float angle = normalizedTime * MathF.PI * 2;
            float lightIntensity = (((dayCycle + 1.0f) / 2f) + 0.1f) * 2;  // Convert sine range (-1 to 1) to (0 to 1)

            lightColor.X = lightIntensity * 1.0f;//R
            lightColor.Y = lightIntensity * 0.7f;//G
            lightColor.Z = lightIntensity * 0.3f;//B

            // The ambient light is less intensive than the diffuse light in order to make it less dominant
            ambientColor = lightColor * new Vector3(0.2f);
            diffuseColor = lightColor * new Vector3(0.5f);

            shaderManager.GetShaderProgram("Terrain")
                .Bind()
                .SetVector3Uniform("light.position", _lightPos)
                .SetVector3Uniform("light.ambient", ambientColor)
                .SetVector3Uniform("light.diffuse", diffuseColor)
                .SetVector3Uniform("light.specular", new Vector3(1.0f, 1.0f, 1.0f));



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

            shaderManager.GetShaderProgram("Lighting")
                .Bind()
                .SetVector3Uniform("light.position", _lightPos);

            if (!IsMenuRendered())
                sunlightViewMatrix = Matrix4.LookAt(_lightPos, new(0, 0, 0), Vector3.UnitY);
            else
                sunlightViewMatrix = Matrix4.LookAt(_lightPos, GetPlayer().GetPosition(), Vector3.UnitY);

            float dist1 = RegionManager.CHUNK_BOUNDS * RegionManager.GetRenderDistance();
            sunlightProjectionMatrix = Matrix4.CreateOrthographicOffCenter(-dist1, dist1, -dist1, dist1 , 1f, dist1);

            lightSpaceMatrix = sunlightViewMatrix * sunlightProjectionMatrix;


            shaderManager.GetShaderProgram("Lighting")
                .SetMatrixUniform("lightViewMatrix", sunlightViewMatrix)
                .SetMatrixUniform("lightProjMatrix", sunlightProjectionMatrix)
                .SetMatrixUniform("lightModel", Chunk.GetModelMatrix());

            shaderManager.GetShaderProgram("Terrain")

                .SetMatrixUniform("lightViewMatrix", sunlightViewMatrix)
                .SetMatrixUniform("lightProjMatrix", sunlightProjectionMatrix)
                .SetMatrixUniform("lightModel", Chunk.GetModelMatrix());

            shaderManager.GetShaderProgram("Inventory")
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

            if (!IsMenuRendered() && !ImGuiHelper.SHOW_BLOCK_COLOR_PICKER) {
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
                    PAUSE = !PAUSE;

            }

            Vector3 target = new(0, 0, 0);
            if (!ImGuiHelper.SHOW_PLAYER_INVENTORY)
                target = GetPlayer().UpdateViewTarget(out _, out _, out Vector3 blockSpace);

            Chunk actionChunk = RegionManager.GetAndLoadGlobalChunkFromCoords(target);
            Vector3i idx = RegionManager.GetChunkRelativeCoordinates(target);

            //If its a lamp block, display the color picker when the key is pressed to display it
            if (current[Keys.C] && (BlockType)actionChunk.blockData[idx.X, idx.Y, idx.Z] == BlockType.LAMP_BLOCK)
            {
                if (!ImGuiHelper.SHOW_BLOCK_COLOR_PICKER)
                {
                    ImGuiHelper.SHOW_BLOCK_COLOR_PICKER = true;
                    dirX = MouseState.X;
                    dirY = MouseState.Y;
                } else if (ImGuiHelper.SHOW_BLOCK_COLOR_PICKER)
                {
                    ImGuiHelper.SHOW_BLOCK_COLOR_PICKER = false;
                    Console.WriteLine("Set look direction to X: " + dirX + " Y: " + dirY);
                    Player.SetLookDir(dirX, dirY);
                }
            }

            // Show player inventory
            if (current[Keys.E] && !IsMenuRendered())
            {
                ImGuiHelper.SHOW_PLAYER_INVENTORY = !ImGuiHelper.SHOW_PLAYER_INVENTORY;
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

            if (!IsMenuRendered() && !ImGuiHelper.SHOW_PLAYER_INVENTORY)
            {
                Vector3 block = GetPlayer().UpdateViewTarget(out BlockFace playerFacing, out Vector3 blockFace, out Vector3 blockSpace);

                if (e.Button == MouseButton.Left && !ImGuiHelper.IsAnyMenuActive())
                {
                    ColorVector color = new(9, 9, 8);
                    RegionManager.AddBlockToChunk(blockSpace, GetPlayer().GetPlayerBlockType(), color);

                }
                if (e.Button == MouseButton.Right && !ImGuiHelper.IsAnyMenuActive())
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
            Vector3 target = (0, 0, 0);
            
            if (!ImGuiHelper.SHOW_PLAYER_INVENTORY)
                target = GetPlayer().UpdateViewTarget(out _, out _, out _);

            //Cursor state handling
            if (ImGuiHelper.SHOW_PLAYER_INVENTORY && !IsMenuRendered())
            {
                CursorState = CursorState.Normal;
                ImGuiHelper.ShowPlayerInventory(ioptr);
            }
            else if (ImGuiHelper.SHOW_BLOCK_COLOR_PICKER && !IsMenuRendered())
            {
                CursorState = CursorState.Normal;
                ImGuiHelper.ShowBlockColorPicker(target);
                ImGui.OpenPopup("ColorPicker");
            }
            else if (!ImGuiHelper.SHOW_BLOCK_COLOR_PICKER && !IsMenuRendered() && !PAUSE)
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
            shaderManager.GetShaderProgram("Lighting").Bind();
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

            shaderManager.GetShaderProgram("Terrain").Bind();



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
            if (Marshal.SizeOf<BlockFaceInstance>() * (maxBlocksPerChunk * maxChunksInCache * blockFacesPerBlock) != ssboManager.GetSSBO("Terrain").Size)
            {
                Console.WriteLine("Size Change");

                int SSBOResize = Marshal.SizeOf<BlockFaceInstance>() * (maxBlocksPerChunk * maxChunksInCache * blockFacesPerBlock);
                ssboManager.AddSSBO(SSBOResize, 0, "Terrain");
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
            shaderManager.GetShaderProgram("Lighting").Bind();
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

            shaderManager.GetShaderProgram("Terrain").Bind();



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
            shaderManager.CleanupShaders();
            TextureLoader.Unbind();
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);

            //Update ortho matrix for crosshair on window resize
            shaderManager.GetShaderProgram("Terrain").SetMatrixUniform("crosshairOrtho", Matrix4.CreateOrthographic(screenWidth, screenHeight, 0.1f, 10f));

            // Update the opengl viewport
            GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);

            Matrix4 pMatrix = Matrix4.CreatePerspectiveFieldOfView(FOV, (float)e.Width / e.Height, NEAR, FAR);
            shaderManager.GetShaderProgram("Terrain").SetMatrixUniform("projectionMatrix", pMatrix);

            // Tell ImGui of the new size
            _UIController.WindowResized(ClientSize.X, ClientSize.Y);
        }

        public static Player GetPlayer() {
            player ??= new();

            return player;
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
                    Console.WriteLine("Processing include: " + line);
                    var match = GLSLIncludeRegex().Match(line);
                    Console.WriteLine("IsMatch: " + match.Success);
                    if (match.Success)
                    {
                        string includePath = match.Groups[1].Value;
                        string resolvedPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(fullPath)!, includePath));
                        Console.WriteLine("IncludePath: " + includePath);
                        Console.WriteLine("ResolvedPath: " + resolvedPath);
                        Console.WriteLine();
                        sb.AppendLine(ProcessShaderIncludes(resolvedPath));
                        
                    }
                    else
                    {
                        throw new Exception($"Invalid include directive: {line}");
                    }
                    
                }
                else
                {

                    sb.AppendLine(line);
                    Console.WriteLine("Appended line: " + line);
                }
            }

            return sb.ToString();
        }


        public static int GetAndIncrementNextFaceIndex()
        {
            return Interlocked.Increment(ref _nextFaceIndex) - 1;
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
