public enum ObjectState
{
    RawMaterial,  // Initial state when placed (replaces "unlocked")
    Refined,      // After using refining tool (replaces "locked")
    Damaged       // After damage mechanic is applied (Phase 4+)
}