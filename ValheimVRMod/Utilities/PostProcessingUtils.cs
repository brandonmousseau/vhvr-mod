using System;
using UnityEngine;
using UnityEngine.PostProcessing;

namespace ValheimVRMod.Utilities
{
    /// <summary>
    /// From Post Processing Stack v2 to add VR support to some effects
    /// </summary>
    static class PostProcessingUtils
    {
        /// <summary>
        /// Gets a jittered perspective projection matrix for a given camera.
        /// </summary>
        /// <param name="camera">The camera to build the projection matrix for</param>
        /// <param name="offset">The jitter offset</param>
        /// <returns>A jittered projection matrix</returns>
        public static Matrix4x4 GetJitteredPerspectiveProjectionMatrix(Camera camera, Vector2 offset)
        {
            float near = camera.nearClipPlane;
            float far = camera.farClipPlane;

            float vertical = Mathf.Tan(0.5f * Mathf.Deg2Rad * camera.fieldOfView) * near;
            float horizontal = vertical * camera.aspect;

            offset.x *= horizontal / (0.5f * camera.pixelWidth);
            offset.y *= vertical / (0.5f * camera.pixelHeight);

            var matrix = camera.projectionMatrix;

            matrix[0, 2] += offset.x / horizontal;
            matrix[1, 2] += offset.y / vertical;

            return matrix;
        }

        /// <summary>
        /// Gets a jittered orthographic projection matrix for a given camera.
        /// </summary>
        /// <param name="camera">The camera to build the orthographic matrix for</param>
        /// <param name="offset">The jitter offset</param>
        /// <returns>A jittered projection matrix</returns>
        public static Matrix4x4 GetJitteredOrthographicProjectionMatrix(Camera camera, Vector2 offset)
        {
            float vertical = camera.orthographicSize;
            float horizontal = vertical * camera.aspect;

            offset.x *= horizontal / (0.5f * camera.pixelWidth);
            offset.y *= vertical / (0.5f * camera.pixelHeight);

            float left = offset.x - horizontal;
            float right = offset.x + horizontal;
            float top = offset.y + vertical;
            float bottom = offset.y - vertical;

            return Matrix4x4.Ortho(left, right, bottom, top, camera.nearClipPlane, camera.farClipPlane);
        }

        /// <summary>
        /// Gets a jittered perspective projection matrix from an original projection matrix.
        /// </summary>
        /// <param name="context">The current render context</param>
        /// <param name="origProj">The original projection matrix</param>
        /// <param name="jitter">The jitter offset</param>
        /// <returns>A jittered projection matrix</returns>
        public static Matrix4x4 GenerateJitteredProjectionMatrixFromOriginal(PostProcessingContext context, Matrix4x4 origProj, Vector2 jitter)
        {
            var planes = origProj.decomposeProjection;

            float vertFov = Math.Abs(planes.top) + Math.Abs(planes.bottom);
            float horizFov = Math.Abs(planes.left) + Math.Abs(planes.right);

            var planeJitter = new Vector2(jitter.x * horizFov / context.width,
                                          jitter.y * vertFov / context.height);

            planes.left += planeJitter.x;
            planes.right += planeJitter.x;
            planes.top += planeJitter.y;
            planes.bottom += planeJitter.y;

            var jitteredMatrix = Matrix4x4.Frustum(planes);

            return jitteredMatrix;
        }

        
        /// <returns>Builds a quad, usefull for fullscreen mesh drawn on a 1,1 quad</returns>
        public static Mesh BuildQuad (float width, float height)
        {
            Mesh mesh = new Mesh ();
            
            // Setup vertices
            Vector3[] newVertices = new Vector3[4];
            float halfHeight = height * 0.5f;
            float halfWidth = width * 0.5f;
            newVertices [0] = new Vector3 (-halfWidth, -halfHeight, 0);
            newVertices [1] = new Vector3 (-halfWidth, halfHeight, 0);
            newVertices [2] = new Vector3 (halfWidth, -halfHeight, 0);
            newVertices [3] = new Vector3 (halfWidth, halfHeight, 0);
            
            // Setup UVs
            Vector2[] newUVs = new Vector2[newVertices.Length];
            newUVs [0] = new Vector2 (0, 0);
            newUVs [1] = new Vector2 (0, 1);
            newUVs [2] = new Vector2 (1, 0);
            newUVs [3] = new Vector2 (1, 1);
            
            // Setup triangles
            int[] newTriangles = new int[] { 0, 1, 2, 3, 2, 1 };
            
            // Setup normals
            Vector3[] newNormals = new Vector3[newVertices.Length];
            for (int i = 0; i < newNormals.Length; i++) {
                newNormals [i] = Vector3.forward;
            }
            
            // Create quad
            mesh.vertices = newVertices;
            mesh.uv = newUVs;
            mesh.triangles = newTriangles;
            mesh.normals = newNormals;
            
            return mesh;
        }
    }
}