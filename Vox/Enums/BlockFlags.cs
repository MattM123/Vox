namespace Vox.Enums
{
    [Flags]
    public enum BlockFlags
    {
        None            = 0,
        
        // Blocks that do not exist unless placed by the player
        Natural         = 1 << 0,
        
        // Block types that are generated with the terrain, not placed by the player. 
        NonNatural     = 1 << 1,
        
        // Block that is instead rendered as a billboard, where the faces are instead rendered in a cross shape
        Billboard       = 1 << 2,
        
        // Blocks that are surface decoration and will only generate right above of the terrain, such as plants
        SurfaceDeco    = 1 << 3,
        
        // Block is intangible and does not have collision detection
        Passthrough     = 1 << 4
    }
}
