// WebGL2-Renderer für Runenruf im Browser. Shader von GLSL 330 core nach ES 300 portiert.
// Wird von Blazor (Spiel.razor) über JSInterop aufgerufen: runenruf.render(canvasId, pos, nor, idx).
(function () {
  const VS = `#version 300 es
    layout(location=0) in vec3 position;
    layout(location=1) in vec3 normale;
    uniform mat4 mvp;
    out vec3 vNormale; out float vHoehe;
    void main(){ gl_Position = mvp * vec4(position,1.0); vNormale = normale; vHoehe = position.y; }`;

  const FS = `#version 300 es
    precision highp float;
    in vec3 vNormale; in float vHoehe; out vec4 farbe;
    void main(){
      vec3 licht = normalize(vec3(0.4,0.8,0.3));               // warme Abendsonne (Stil-Bibel)
      float diffus = max(dot(normalize(vNormale), licht), 0.0);
      vec3 gras = vec3(0.34,0.43,0.23);
      vec3 fels = vec3(0.42,0.38,0.31);
      vec3 grund = mix(gras, fels, clamp(vHoehe/14.0, 0.0, 1.0));
      vec3 warm = vec3(1.0,0.92,0.78);
      farbe = vec4(grund * (0.35 + 0.65*diffus) * warm, 1.0);
    }`;

  // --- Minimale 4x4-Matrix-Mathematik (spaltenweise, wie OpenGL) ---
  function mul(a, b) {
    const o = new Float32Array(16);
    for (let c = 0; c < 4; c++) for (let r = 0; r < 4; r++)
      o[c*4+r] = a[r]*b[c*4] + a[4+r]*b[c*4+1] + a[8+r]*b[c*4+2] + a[12+r]*b[c*4+3];
    return o;
  }
  function perspektive(fovy, aspect, n, f) {
    const t = 1/Math.tan(fovy/2);
    return new Float32Array([t/aspect,0,0,0, 0,t,0,0, 0,0,(f+n)/(n-f),-1, 0,0,(2*f*n)/(n-f),0]);
  }
  function sub(a,b){return [a[0]-b[0],a[1]-b[1],a[2]-b[2]];}
  function norm(a){const l=Math.hypot(a[0],a[1],a[2])||1;return [a[0]/l,a[1]/l,a[2]/l];}
  function cross(a,b){return [a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];}
  function dot(a,b){return a[0]*b[0]+a[1]*b[1]+a[2]*b[2];}
  function lookAt(eye, ctr, up) {
    const f = norm(sub(ctr, eye)), s = norm(cross(f, up)), u = cross(s, f);
    return new Float32Array([
      s[0],u[0],-f[0],0, s[1],u[1],-f[1],0, s[2],u[2],-f[2],0,
      -dot(s,eye),-dot(u,eye),dot(f,eye),1]);
  }

  function shader(gl, typ, quelle) {
    const s = gl.createShader(typ);
    gl.shaderSource(s, quelle); gl.compileShader(s);
    if (!gl.getShaderParameter(s, gl.COMPILE_STATUS)) throw new Error(gl.getShaderInfoLog(s));
    return s;
  }

  // GL-Ressourcen pro Canvas einmalig anlegen und wiederverwenden (kein Leak bei wiederholtem Rendern).
  const cache = new Map();
  function init(cv) {
    const gl = cv.getContext("webgl2");
    if (!gl) return null;
    const prog = gl.createProgram();
    gl.attachShader(prog, shader(gl, gl.VERTEX_SHADER, VS));
    gl.attachShader(prog, shader(gl, gl.FRAGMENT_SHADER, FS));
    gl.linkProgram(prog);
    if (!gl.getProgramParameter(prog, gl.LINK_STATUS)) { console.error(gl.getProgramInfoLog(prog)); return null; }
    const vao = gl.createVertexArray();
    gl.bindVertexArray(vao);
    const vbo = gl.createBuffer(), nbo = gl.createBuffer(), ebo = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, vbo); gl.vertexAttribPointer(0,3,gl.FLOAT,false,0,0); gl.enableVertexAttribArray(0);
    gl.bindBuffer(gl.ARRAY_BUFFER, nbo); gl.vertexAttribPointer(1,3,gl.FLOAT,false,0,0); gl.enableVertexAttribArray(1);
    gl.enable(gl.DEPTH_TEST);
    gl.clearColor(0.45, 0.55, 0.70, 1);
    return { gl, prog, vao, vbo, nbo, ebo, mvpLoc: gl.getUniformLocation(prog, "mvp") };
  }

  window.runenruf = {
    render(canvasId, positions, normals, indices) {
      const cv = document.getElementById(canvasId);
      if (!cv) return false;
      let st = cache.get(cv);
      if (!st) { st = init(cv); if (!st) { console.error("WebGL2 nicht verfügbar"); return false; } cache.set(cv, st); }
      const { gl, prog, vao, vbo, nbo, ebo, mvpLoc } = st;

      gl.bindVertexArray(vao);
      gl.bindBuffer(gl.ARRAY_BUFFER, vbo); gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(positions), gl.STATIC_DRAW);
      gl.bindBuffer(gl.ARRAY_BUFFER, nbo); gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(normals), gl.STATIC_DRAW);
      gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, ebo); gl.bufferData(gl.ELEMENT_ARRAY_BUFFER, new Uint32Array(indices), gl.STATIC_DRAW);

      gl.viewport(0, 0, cv.width, cv.height);
      gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);
      const mvp = mul(perspektive(Math.PI/4, cv.width/cv.height, 0.1, 500), lookAt([50,55,105], [50,0,45], [0,1,0]));
      gl.useProgram(prog);
      gl.uniformMatrix4fv(mvpLoc, false, mvp);
      gl.drawElements(gl.TRIANGLES, indices.length, gl.UNSIGNED_INT, 0);
      return true;
    },
  };
})();
