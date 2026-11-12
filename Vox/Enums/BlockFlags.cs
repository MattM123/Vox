namespace Vox.Enums
{
    [Flags]
    public enum BlockFlags
    {
        NONE            = 0,
        
        // Blocks that do not exist unless placed by the player
        NATURAL         = 1 << 0,
        
        // Block types that are generated with the terrain, not placed by the player. 
        NON_NATURAL     = 1 << 1,
        
        // Block that is instead rendered as a billboard, where the faces are instead rendered in a cross shape
        BILLBOARD       = 1 << 2,
        
        // Blocks that are surface decoration and will only generate right above of the terrain, such as plants
        SURFACE_DECO    = 1 << 3,
        
        // Block is intangible and does not have collision detection
        PASSTHROUGH     = 1 << 4
    }
}
