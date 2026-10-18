using System;
using System.Numerics;
using System.Runtime.InteropServices;
using OpenToolkit.Graphics.OpenGL4;
using Robust.Shared.Maths;

namespace Robust.Client.Graphics.Clyde
{
    internal partial class Clyde
    {
        // Один исходник на обе стадии: _compileProgram добавляет #define VERTEX_SHADER / FRAGMENT_SHADER.
        private const string Scene3DShaderSource = @"
#ifdef GL_ES
precision highp float;
#endif
#ifdef VERTEX_SHADER
in vec3 aPos;
in vec4 aColor;
uniform mat4 uViewProj;
out vec4 vColor;
void main()
{
    gl_Position = uViewProj * vec4(aPos, 1.0);
    vColor = aColor;
}
#endif
#ifdef FRAGMENT_SHADER
in vec4 vColor;
out vec4 fragColor;
void main()
{
    fragColor = vColor;
}
#endif
";

        private static readonly (string, uint)[] Scene3DAttribLocations =
        {
            ("aPos", 0),
            ("aColor", 1),
        };

        private GLShaderProgram? _program3D;
        private GLBuffer? _vbo3D;
        private uint _vao3D;
        private bool _scene3DFailed;

        private bool ShouldRender3D(IEye eye)
        {
            return Scene3D.Enabled
                   && !_scene3DFailed
                   && Scene3D.TargetEye != null
                   && ReferenceEquals(eye, Scene3D.TargetEye);
        }

        private unsafe bool EnsureScene3DResources()
        {
            if (_program3D != null)
                return true;

            try
            {
                _program3D = _compileProgram(
                    Scene3DShaderSource, Scene3DShaderSource, Scene3DAttribLocations, "scene3d", includeLib: false);
            }
            catch (ShaderCompilationException e)
            {
                _clydeSawmill.Error($"Scene3D: shader compilation failed, disabling 3D: {e}");
                _scene3DFailed = true;
                return false;
            }

            _vao3D = GenVertexArray();
            BindVertexArray(_vao3D);
            CheckGlError();

            _vbo3D = new GLBuffer(
                this, BufferTarget.ArrayBuffer, BufferUsageHint.StreamDraw, 4096 * sizeof(Vertex3D), "Scene3D VBO");
            _vbo3D.Use();

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, sizeof(Vertex3D), 0);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, sizeof(Vertex3D), 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);
            CheckGlError();

            BindVertexArray(BatchVAO.Handle);
            CheckGlError();
            return true;
        }

        private void Render3DScene(Viewport viewport)
        {
            if (!EnsureScene3DResources())
                return;

            // Завершить всё, что накопилось в очереди 2D-рендера, и сбросить её состояние.
            FlushRenderQueue();

            var sky = Color.FromSrgb(Scene3D.SkyColor);

            IsBlending = false;
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Less);
            GL.DepthMask(true);
            GL.ClearDepth(1);
            GL.ClearColor(sky.R, sky.G, sky.B, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            CheckGlError();

            var vertices = Scene3D.Vertices;
            if (vertices.Count > 0)
            {
                var size = viewport.RenderTarget.Size;
                var aspect = size.X / (float) Math.Max(1, size.Y);

                var cp = MathF.Cos(Scene3D.Pitch);
                var forward = new Vector3(
                    MathF.Sin(Scene3D.Yaw) * cp,
                    MathF.Sin(Scene3D.Pitch),
                    -MathF.Cos(Scene3D.Yaw) * cp);

                var view = Matrix4x4.CreateLookAt(
                    Scene3D.CameraPosition, Scene3D.CameraPosition + forward, Vector3.UnitY);
                var proj = Matrix4x4.CreatePerspectiveFieldOfView(
                    Scene3D.FovDegrees * MathF.PI / 180f, aspect, 0.05f, 300f);

                // System.Numerics — «строчная» конвенция: view * proj; в шейдер грузим без транспонирования.
                var viewProj = view * proj;

                _program3D!.Use();
                _program3D.SetUniform("uViewProj", viewProj, false);

                BindVertexArray(_vao3D);
                _vbo3D!.Reallocate(CollectionsMarshal.AsSpan(vertices));

                GL.DrawArrays(PrimitiveType.Triangles, 0, vertices.Count);
                CheckGlError();
                _debugStats.LastGLDrawCalls += 1;
            }

            // Вернуть состояние, которое ожидает остальной рендерер.
            GL.DepthMask(false);
            GL.Disable(EnableCap.DepthTest);
            IsBlending = true;
            BindVertexArray(BatchVAO.Handle);
            CheckGlError();
        }
    }
}
