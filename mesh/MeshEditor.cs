﻿using System;
using System.Collections.Generic;
using System.Diagnostics;


namespace g3
{
    // This class implements various ways to do low-level edits to a mesh, in
    // a robust/reliable way.
    //   - if operations fail in-progress, we try to back them out
    //   - (?)
    //
    // 
    public class MeshEditor
    {
        public DMesh3 Mesh;


        public MeshEditor(DMesh3 mesh)
        {
            Mesh = mesh;
        }



        public virtual int[] AddTriangleFan_OrderedVertexLoop(int center, int[] vertex_loop, int group_id = -1)
        {
            int N = vertex_loop.Length;
            int[] new_tris = new int[N];

            int i = 0;
            for ( i = 0; i < N; ++i ) {
                int a = vertex_loop[i];
                int b = vertex_loop[(i + 1) % N];

                Index3i newT = new Index3i(center, b, a);
                int new_tid = Mesh.AppendTriangle(newT, group_id);
                if (new_tid == DMesh3.InvalidID)
                    goto operation_failed;

                new_tris[i] = new_tid;
            }

            return new_tris;


            operation_failed:
                // remove what we added so far
                if (i > 0) {
                    if (remove_triangles(new_tris, i) == false)
                        throw new Exception("MeshConstructor.AddTriangleFan_OrderedVertexLoop: failed to add fan, and also falied to back out changes.");
                }
                return null;
        }





        public virtual int[] AddTriangleFan_OrderedEdgeLoop(int center, int[] edge_loop, int group_id = -1)
        {
            int N = edge_loop.Length;
            int[] new_tris = new int[N];

            int i = 0;
            for ( i = 0; i < N; ++i ) {
                if (Mesh.edge_is_boundary(edge_loop[i]) == false)
                    goto operation_failed;

                Index2i ev = Mesh.GetOrientedBoundaryEdgeV(edge_loop[i]);
                int a = ev.a, b = ev.b;

                Index3i newT = new Index3i(center, b, a);
                int new_tid = Mesh.AppendTriangle(newT, group_id);
                if (new_tid == DMesh3.InvalidID)
                    goto operation_failed;

                new_tris[i] = new_tid;
            }

            return new_tris;


            operation_failed:
                // remove what we added so far
                if (i > 0) {
                    if (remove_triangles(new_tris, i) == false)
                        throw new Exception("MeshConstructor.AddTriangleFan_OrderedEdgeLoop: failed to add fan, and also falied to back out changes.");
                }
                return null;
        }







        // [TODO] cannot back-out this operation right now
        //
        // Remove list of triangles. Values of triangles[] set to InvalidID are ignored.
        public bool RemoveTriangles(int[] triangles, bool bRemoveIsolatedVerts)
        {
            bool bAllOK = true;
            for (int i = 0; i < triangles.Length; ++i ) {
                if (triangles[i] == DMesh3.InvalidID)
                    continue;

                bool bOK = Mesh.RemoveTriangle(triangles[i], bRemoveIsolatedVerts, false);
                if (!bOK)
                    bAllOK = false;
            }
            return bAllOK;
        }





        // Assumption here is that Submesh has been modified, but boundary loop has
        // been preserved, and that old submesh has already been removed from this mesh.
        // So, we just have to append new vertices and then rewrite triangles
        // If new_tris or new_verts is non-null, we will return this info.
        // new_tris should be set to TriangleCount (ie it is not necessarily a map)
        // For new_verts, if we used an existing bdry vtx instead, we set the value to -(existing_index+1),
        // otherwise the value is new_index (+1 is to handle 0)
        public void ReinsertSubmesh(DSubmesh3 sub, ref int[] new_tris, out IndexMap SubToNewV)
        {
            if (sub.BaseBorderV == null)
                throw new Exception("MeshEditor.ReinsertSubmesh: Submesh does not have required boundary info. Call ComputeBoundaryInfo()!");

            DMesh3 submesh = sub.SubMesh;

            IndexFlagSet done_v = new IndexFlagSet(submesh.MaxVertexID, submesh.TriangleCount/2);
            SubToNewV = new IndexMap(submesh.MaxVertexID, submesh.VertexCount);

            int nti = 0;
            int NT = submesh.MaxTriangleID;
            for (int ti = 0; ti < NT; ++ti ) {
                if (submesh.IsTriangle(ti) == false)
                    continue;

                Index3i sub_t = submesh.GetTriangle(ti);
                int gid = submesh.GetTriangleGroup(ti);

                Index3i new_t = Index3i.Zero;
                for ( int j = 0; j < 3; ++j ) {
                    int sub_v = sub_t[j];
                    int new_v = -1;
                    if (done_v[sub_v] == false) {

                        // first check if this is a boundary vtx on submesh and maps to a bdry vtx on base mesh
                        if (submesh.vertex_is_boundary(sub_v)) {
                            int base_v = (sub_v < sub.SubToBaseV.size) ? sub.SubToBaseV[sub_v] : -1;
                            if ( base_v >= 0 && Mesh.IsVertex(base_v) && sub.BaseBorderV[base_v] == true ) { 
                                // [RMS] this should always be true, but assert in tests to find out
                                Debug.Assert(Mesh.vertex_is_boundary(base_v));
                                if (Mesh.vertex_is_boundary(base_v)) {
                                    new_v = base_v;
                                }
                            }
                        }

                        // if that didn't happen, append new vtx
                        if ( new_v == -1 ) {
                            new_v = Mesh.AppendVertex(submesh, sub_v);
                        }

                        SubToNewV[sub_v] = new_v;
                        done_v[sub_v] = true;

                    } else 
                        new_v = SubToNewV[sub_v];

                    new_t[j] = new_v;
                }

                int new_tid = Mesh.AppendTriangle(new_t, gid);
                if (new_tris != null)
                    new_tris[nti++] = new_tid;
            }


        }







        // this is for backing out changes we have made...
        bool remove_triangles(int[] tri_list, int count)
        {
            for (int i = 0; i < count; ++i) {
                bool bOK = Mesh.RemoveTriangle(tri_list[i], false, false);
                if (!bOK)
                    return false;
            }
            return true;
        }


    }
}
