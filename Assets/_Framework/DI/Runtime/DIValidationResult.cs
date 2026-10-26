using System;
using System.Collections.Generic;
using System.Linq;

namespace RPGFramework.DI
{
    public enum DIValidationIssueKind
    {
        MissingDependency,
        CircularDependency,
        NoPublicConstructor
    }

    /// <summary>
    /// One problem found by <see cref="IDIContainer.Validate"/>.
    /// </summary>
    public sealed class DIValidationIssue
    {
        public DIValidationIssue(DIValidationIssueKind kind, Type serviceType, Type implementationType, Type dependencyType, string message)
        {
            Kind = kind;
            ServiceType = serviceType;
            ImplementationType = implementationType;
            DependencyType = dependencyType;
            Message = message;
        }

        public DIValidationIssueKind Kind { get; }
        public Type ServiceType { get; }
        public Type ImplementationType { get; }

        /// <summary>
        /// The unregistered constructor parameter type for <see cref="DIValidationIssueKind.MissingDependency"/>.
        /// </summary>
        public Type DependencyType { get; }

        public string Message { get; }

        public override string ToString() => $"[{Kind}] {Message}";
    }

    /// <summary>
    /// Outcome of validating a container's registrations.
    /// </summary>
    public sealed class DIValidationResult
    {
        public DIValidationResult(IReadOnlyList<DIValidationIssue> issues)
        {
            Issues = issues ?? Array.Empty<DIValidationIssue>();
        }

        public IReadOnlyList<DIValidationIssue> Issues { get; }

        public bool IsValid => Issues.Count == 0;

        /// <summary>
        /// Throws <see cref="InvalidOperationException"/> listing every issue when the container is invalid.
        /// </summary>
        public void ThrowIfInvalid()
        {
            if (!IsValid)
                throw new InvalidOperationException("DI container is invalid:\n" + string.Join("\n", Issues.Select(i => i.ToString())));
        }

        public override string ToString() => IsValid ? "DI container is valid" : string.Join("\n", Issues.Select(i => i.ToString()));
    }
}
