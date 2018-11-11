using System;

namespace Microsoft.Diagnostics
{
    /// <summary>
    /// Indicates that the marked element is performance critical and should be
    /// analyzed for heap allocations.
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
    public class PerformanceCriticalAttribute : Attribute
    {
        public AllocationKind Allocations { get; set; } = AllocationKind.All;
    }

    [Flags]
    public enum AllocationKind
    {
        Explicit = 1,
        TheRest = 2,
        All = Explicit | TheRest
    }
}