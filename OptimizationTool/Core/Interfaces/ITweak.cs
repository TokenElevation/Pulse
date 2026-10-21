namespace OptimizationTool.Core.Interfaces
{
    public interface ITweak
    {
        string Id { get; }
        string Title { get; }
        string Description { get; }

        // true for utilities that run once false for others
        bool IsAction => false;

        bool IsApplied();
        void Apply();
        void Revert();
    }
}