using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System.Numerics;

namespace Runenruf.Engine;

/// <summary>
/// OpenGL-3.3-Backend auf Silk.NET (adr-001): ein Fenster, warmes Licht,
/// RTS-Kamera ueber dem Weltsplitter. Laeuft auf macOS (GL 4.1), Windows, Linux.
/// </summary>
public sealed class GlBackend(int breite = 1920, int hoehe = 1080, GlProfil profil = GlProfil.Desktop) : IRenderBackend
{
    private IWindow? _fenster;
    private GL? _gl;
    private uint _programm, _vao, _vbo, _nbo, _ebo;
    private int _indexAnzahl;
    private Netz? _wartendesTerrain;
    private bool _laeuft;

    public FrameStatistik Statistik { get; private set; }

    private const string VertexShader = """
        #version 330 core
        layout(location = 0) in vec3 position;
        layout(location = 1) in vec3 normale;
        uniform mat4 mvp;
        out vec3 vNormale;
        out float vHoehe;
        void main() {
            gl_Position = mvp * vec4(position, 1.0);
            vNormale = normale;
            vHoehe = position.y;
        }
        """;

    private const string FragmentShader = """
        #version 330 core
        in vec3 vNormale;
        in float vHoehe;
        out vec4 farbe;
        void main() {
            // Warme Abendsonne — die SpellForce-Stimmung (Stil-Bibel)
            vec3 licht = normalize(vec3(0.4, 0.8, 0.3));
            float diffus = max(dot(normalize(vNormale), licht), 0.0);
            vec3 gras = vec3(0.34, 0.43, 0.23);
            vec3 fels = vec3(0.42, 0.38, 0.31);
            vec3 grund = mix(gras, fels, clamp(vHoehe / 14.0, 0.0, 1.0));
            vec3 warm = vec3(1.0, 0.92, 0.78);
            farbe = vec4(grund * (0.35 + 0.65 * diffus) * warm, 1.0);
        }
        """;

    public void Initialisiere()
    {
        _fenster = Window.Create(GlKontext.Optionen(profil, breite, hoehe));
        _fenster.Load += BeiLaden;
        _fenster.Render += BeiRender;
        _fenster.Closing += () => _laeuft = false;
        _fenster.Initialize();
        _laeuft = true;
    }

    public void LadeTerrain(Netz netz) => _wartendesTerrain = netz;

    public bool Frame(double deltaSekunden)
    {
        if (_fenster is null) throw new InvalidOperationException("Initialisiere() zuerst.");
        if (!_laeuft) return false;
        _fenster.DoEvents();
        _fenster.DoRender();
        return _laeuft && !_fenster.IsClosing;
    }

    private void BeiLaden()
    {
        _gl = GL.GetApi(_fenster!);
        _gl.Enable(EnableCap.DepthTest);
        _gl.ClearColor(0.45f, 0.55f, 0.70f, 1f);
        _programm = BaueProgramm(_gl, VertexShader, FragmentShader);
    }

    private unsafe void BeiRender(double delta)
    {
        var gl = _gl!;
        if (_wartendesTerrain is { } netz)
        {
            LadeNetzInGpu(gl, netz);
            _wartendesTerrain = null;
        }
        gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        if (_indexAnzahl == 0 || _programm == 0) return; // kein Render vor gültigem Shader/Mesh

        gl.UseProgram(_programm);
        var mvp = Kamera() * Projektion();
        int ort = gl.GetUniformLocation(_programm, "mvp");
        gl.UniformMatrix4(ort, 1, false, (float*)&mvp);
        gl.BindVertexArray(_vao);
        gl.DrawElements(PrimitiveType.Triangles, (uint)_indexAnzahl, DrawElementsType.UnsignedInt, null);
        Statistik = new FrameStatistik(_indexAnzahl / 3, 1);
    }

    private Matrix4x4 Kamera() =>
        Matrix4x4.CreateLookAt(new Vector3(50f, 55f, 105f), new Vector3(50f, 0f, 45f), Vector3.UnitY);

    private Matrix4x4 Projektion() =>
        Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4f, breite / (float)hoehe, 0.1f, 500f);

    private unsafe void LadeNetzInGpu(GL gl, Netz netz)
    {
        if (_vao != 0) // vorheriges Terrain freigeben (kein GPU-Leak bei Reload)
        {
            gl.DeleteVertexArray(_vao);
            gl.DeleteBuffer(_vbo); gl.DeleteBuffer(_nbo); gl.DeleteBuffer(_ebo);
        }
        _vao = gl.GenVertexArray();
        gl.BindVertexArray(_vao);

        _vbo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (float* p = netz.Positionen)
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(netz.Positionen.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);
        gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), null);
        gl.EnableVertexAttribArray(0);

        _nbo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ArrayBuffer, _nbo);
        fixed (float* p = netz.Normalen)
            gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(netz.Normalen.Length * sizeof(float)), p, BufferUsageARB.StaticDraw);
        gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), null);
        gl.EnableVertexAttribArray(1);

        _ebo = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        fixed (uint* p = netz.Indizes)
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(netz.Indizes.Length * sizeof(uint)), p, BufferUsageARB.StaticDraw);

        _indexAnzahl = netz.Indizes.Length;
    }

    private static uint BaueProgramm(GL gl, string vertexQuelle, string fragmentQuelle)
    {
        uint Kompiliere(ShaderType typ, string quelle)
        {
            uint s = gl.CreateShader(typ);
            gl.ShaderSource(s, quelle);
            gl.CompileShader(s);
            gl.GetShader(s, ShaderParameterName.CompileStatus, out int ok);
            if (ok == 0) throw new InvalidOperationException($"{typ}: {gl.GetShaderInfoLog(s)}");
            return s;
        }
        uint v = Kompiliere(ShaderType.VertexShader, vertexQuelle);
        uint f = Kompiliere(ShaderType.FragmentShader, fragmentQuelle);
        uint programm = gl.CreateProgram();
        gl.AttachShader(programm, v);
        gl.AttachShader(programm, f);
        gl.LinkProgram(programm);
        gl.GetProgram(programm, ProgramPropertyARB.LinkStatus, out int gelinkt);
        if (gelinkt == 0) throw new InvalidOperationException($"Link: {gl.GetProgramInfoLog(programm)}");
        gl.DeleteShader(v);
        gl.DeleteShader(f);
        return programm;
    }

    public void Dispose()
    {
        if (_gl is { } gl)
        {
            if (_vao != 0) { gl.DeleteVertexArray(_vao); gl.DeleteBuffer(_vbo); gl.DeleteBuffer(_nbo); gl.DeleteBuffer(_ebo); }
            if (_programm != 0) gl.DeleteProgram(_programm);
        }
        _fenster?.Dispose();
        _laeuft = false;
    }
}
