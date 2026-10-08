namespace Featurewise;

/// <summary>
/// Names a feature in serialized links, in place of its class name.
/// </summary>
/// <param name="name">The feature's name, unique among registered features.</param>
/// <remarks>
/// Serialized links outlive the code that wrote them: clients hold them and queues and databases
/// store them. Naming a feature explicitly lets its class be renamed or moved without breaking them.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class FeatureNameAttribute(string name) : Attribute
{
    /// <summary>The feature's name.</summary>
    public string Name { get; } = name;
}
