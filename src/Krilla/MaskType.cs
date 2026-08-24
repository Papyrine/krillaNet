namespace Krilla;

/// <summary>
/// How a mask's content is interpreted.
/// </summary>
public enum MaskType
{
    /// <summary>Brightness becomes opacity: white shows, black hides.</summary>
    Luminosity = 0,

    /// <summary>The mask's own alpha channel becomes opacity.</summary>
    Alpha = 1
}