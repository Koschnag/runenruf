using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Runenruf.Engine;

/// <summary>Welchen GL-Kontext eine Plattform fährt (spec-fenster, risk-gl-macos).</summary>
public enum GlProfil
{
    /// <summary>Mac/Windows/Linux-Desktop: OpenGL 3.3 Core (macOS via ForwardCompatible bis 4.1).</summary>
    Desktop,
    /// <summary>Min-Spec/Mobil (Raspberry Pi 5, GLES-only): OpenGL ES 3.0.</summary>
    Eingebettet,
}

/// <summary>
/// spec-fenster: die reine, testbare Wahl des GL-/ES-Kontexts. GlBackend baut sein
/// Fenster aus genau diesen Werten — der Test prüft denselben Kontrakt, ohne ein
/// echtes Display zu brauchen (das gibt es in CI nicht).
/// </summary>
public static class GlKontext
{
    /// <summary>Grafik-API je Profil: Desktop GL 3.3 Core, eingebettet GL ES 3.0.</summary>
    public static GraphicsAPI Api(GlProfil profil) => profil switch
    {
        GlProfil.Desktop =>
            new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, 3)),
        GlProfil.Eingebettet =>
            new GraphicsAPI(ContextAPI.OpenGLES, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 0)),
        _ => throw new ArgumentOutOfRangeException(nameof(profil), profil, "Unbekanntes GL-Profil"),
    };

    /// <summary>Fenster-Optionen, die Initialisiere() dem Fenster gibt — selber Pfad wie der Test.</summary>
    public static WindowOptions Optionen(GlProfil profil, int breite, int hoehe) =>
        WindowOptions.Default with
        {
            Size = new Vector2D<int>(breite, hoehe),
            Title = "Runenruf",
            API = Api(profil),
        };
}
