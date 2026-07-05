/// <summary>
/// Character gender (3.1.4). Picked first at creation and gates the available races/classes; drives
/// which body model is shown (see <see cref="CharacterRoster"/>) and finally gives the conversation
/// system's <c>&lt;gender&gt;</c> token a real value. Kept a plain enum (no Unity dependency) so the
/// Unity-free <see cref="CharacterSnapshot"/> can carry it across the persistence thread boundary.
/// </summary>
public enum Gender
{
    Male,
    Female,
}
