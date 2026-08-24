namespace Krilla.Html.Diagnostics;

/// <summary>
/// One element's geometry, in the shape a browser's <c>getBoundingClientRect()</c> reports it.
/// </summary>
/// <param name="Selector">The element's selector path.</param>
/// <param name="X">Left edge of the border box, in CSS pixels, document-relative.</param>
/// <param name="Y">Top edge of the border box.</param>
/// <param name="Width">Border box width.</param>
/// <param name="Height">Border box height.</param>
public readonly record struct BoxGeometry(string Selector, float X, float Y, float Width, float Height);