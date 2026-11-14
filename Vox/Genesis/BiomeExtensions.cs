using System.Dynamic;
using Vox.Assets;
using Vox.Enums;

namespace Vox.Genesis;

public static class BiomeExtensions
{
    
    public static BlockType GetGeneratedBlockType(int x, int y, int z, Biome biome, long seed, IAssetLookup assetLookup)
    {
        //Default biome for ones that do not load in time
        if (biome == Biome.None)
            biome = Biome.Grasslands;
        
        //Get a list of block the terrain can be generated from
        List<BlockType> biomeBlockTypes = assetLookup.GetTerrainBlocks(biome);
        
        switch (biome)
        {
            case Biome.Grasslands :
            {
                //Generate noise and normalize from [-1, 1] to [0, 1].
                float normalizedNoise =
                    (OpenSimplex2.Noise3_ImproveXZ(seed, x, y, z) + 1f) * 0.5f;
                
                if (biomeBlockTypes.Count == 0)
                    return BlockType.Lamp;
                
                int index = Math.Clamp(
                    (int)(normalizedNoise * biomeBlockTypes.Count),
                    0,
                    biomeBlockTypes.Count - 1
                );

                return biomeBlockTypes[index];
            }
            
            case Biome.Desert :
            {
              
                //Generate noise and normalize from [-1, 1] to [0, 1].
                float normalizedNoise =
                    (OpenSimplex2.Noise3_ImproveXZ(seed, x, y, z) + 1f) * 0.5f;
                
                if (biomeBlockTypes.Count == 0)
                    return BlockType.Stone;
                
                int index = Math.Clamp(
                    (int)(normalizedNoise * biomeBlockTypes.Count),
                    0,
                    biomeBlockTypes.Count - 1
                );
                return biomeBlockTypes[index];
            }



            default:
                return BlockType.Lamp;
            

        }
    }

    public static BlockType GetSurfaceDecoration(int x, int z, Biome biome, long seed, IAssetLookup assetLookup)
    {
        if (Window.IsMainMenuScreenDisplayed())
            Window.GetMenuSeed();

        List<BlockType> surfaceDeco = assetLookup.GetSurfaceDeco(biome);
        
        const double densityFrequency = 0.05;
        const double spawnFrequency = 0.4;

        float density = OpenSimplex2.Noise2(
            seed + 1,
            x * densityFrequency,
            z * densityFrequency
        );

        float spawnNoise = OpenSimplex2.Noise2(
            seed + 2,
            x * spawnFrequency,
            z * spawnFrequency
        );

        switch (biome)
        {
            case Biome.Grasslands:
            {
                // Normalize noise to [0, 1].
                density = Math.Clamp((density + 1f) * 0.5f, 0f, 1f) * 0.5f;
                float probability = Math.Clamp((spawnNoise + 1f) * 0.5f, 0f, 1f);

                if (surfaceDeco.Count == 0)
                    return BlockType.Air;

                // Use the noise to control local vegetation density.
                if (probability > density)
                    return BlockType.Air;
                
                // Select a decoration deterministically for this coordinate.
                int decoSelection = unchecked((int)(seed + x * 73856093L ^ z * 19349663L));
                Random random = new(decoSelection);
            
                return surfaceDeco[random.Next(surfaceDeco.Count)];
            }
            
            case Biome.Desert :
            {
                // Normalize noise to [0, 1].
                density = (Math.Clamp((density + 1f) * 0.5f, 0f, 1f) * 0.5f) * 0.5f;
                float probability = Math.Clamp((spawnNoise + 1f) * 0.5f, 0f, 1f);

                if (surfaceDeco.Count == 0)
                    return BlockType.Air;

                // Use the noise to control local vegetation density.
                if (probability > density)
                    return BlockType.Air;
                
                // Select a decoration deterministically for this coordinate.
                int decoSelection = unchecked((int)(seed + x * 73856093L ^ z * 19349663L));
                Random random = new(decoSelection);
            
                return surfaceDeco[random.Next(surfaceDeco.Count)];
            }
            
            default:
                return BlockType.DeadBush;
        }
    }
}