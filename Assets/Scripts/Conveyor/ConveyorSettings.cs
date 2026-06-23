using UnityEngine;

[CreateAssetMenu(fileName = "ConveyorSettings", menuName = "Sand/Conveyor Settings")]
public class ConveyorSettings : ScriptableObject
{
    public const float DefaultMoveSpeed = 55f;
    public const float DefaultFullSpeedMultiplier = 1.3f;
    public const float DefaultBeltScrollSpeed = 90f;

    [Min(0.01f)]
    public float moveSpeed = DefaultMoveSpeed;

    [Min(1f)]
    public float fullSpeedMultiplier = DefaultFullSpeedMultiplier;

    [Min(0.01f)]
    public float beltScrollSpeed = DefaultBeltScrollSpeed;
}
