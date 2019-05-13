using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Linq;
using System.Diagnostics;
using System;

public class VoxelChunk : MonoBehaviour
{

    public MeshFilter mf;
    public MeshCollider mc;
    public static int size = 16;
    int index1;
    int index2;
    int index3;
    float[,,] densities = new float[size, size, size];
    int[,,] types = new int[size, size, size];
    List<Vector3> vertices = new List<Vector3>();
    List<Vector3> normals = new List<Vector3>();
    List<int> indices = new List<int>();
    List<Color> colors = new List<Color>();
    VoxelChunk[,,] chunks = new VoxelChunk[3,3,3];
    Vector3 pos;
    public static List<VoxelChunk> loadedChunks = new List<VoxelChunk>();
    public static List<VoxelChunk> loadedVoxels = new List<VoxelChunk>();
    public static List<VoxelChunk> loadedLoads = new List<VoxelChunk>();
    public static List<VoxelChunk> loadedSmooths = new List<VoxelChunk>();
    public static List<VoxelChunk> loadedReloads = new List<VoxelChunk>();
    public static List<VoxelChunk> loadedVoxelChunks = new List<VoxelChunk>();
    bool voxelsLoaded;
    bool chunksLoaded;
    bool voxelsLoadedAround;
    bool chunksLoadedAround;
    bool loading;
    bool loaded;
    bool smoothed;
    bool graphicsLoaded;
    bool reloading;
    bool hasGraphics;
    public static int numThreads = 0;
    public static int maxNumThreads()
    {
        return Environment.ProcessorCount;
    }
    public static bool Done;
    private static Mutex mut = new Mutex();

    void Start()
    {
    }

    public static void RunLoadGraphics()
    {
        {

            VoxelChunk[] load = new VoxelChunk[8];
            for (int i = 0; i < loadedSmooths.Count; i++)
            {
                if (!loadedSmooths[i].graphicsLoaded && !loadedSmooths[i].loading)
                {
                    VoxelChunk c = loadedSmooths[i];
                    for (int ii = 0; ii < load.Length; ii++)
                    {
                        if (load[ii] == null)
                        {
                            load[ii] = c;
                            ii = load.Length;
                        }
                        else
                        if ((c.pos - VoxelWorld.pos).magnitude < (load[ii].pos - VoxelWorld.pos).magnitude)
                        {
                            VoxelChunk cc = c;
                            c = load[ii];
                            load[ii] = cc;
                        }
                    }
                }
            }
            for (int i = 0; i < load.Length; i++)
            {
                if (load[i] != null)
                {
                    load[i].LoadGraphics();
                }
            }
        }
    }
    static bool IntersectsBox(Bounds box, float frustumPadding)
    {

        var center = box.center;
        var extents = box.extents;

        for (int i = 0; i < (VoxelWorld.planes != null ? VoxelWorld.planes.Length : 0); i++)
        {
            Plane plane = VoxelWorld.planes[i];
            var abs = plane.normal;
            abs.x = Mathf.Abs(abs.x);
            abs.y = Mathf.Abs(abs.y);
            abs.z = Mathf.Abs(abs.z);
            var planeNormal = plane.normal;
            var planeDistance = plane.distance;

            float r = extents.x * abs.x + extents.y * abs.y + extents.z * abs.z;
            float s = planeNormal.x * center.x + planeNormal.y * center.y + planeNormal.z * center.z;

            if (s + r < -planeDistance - frustumPadding)
            {
                return false;
            }
        }

        return true;
    }

    public static void Run()
    {
        while (true)
        {
            {

                VoxelChunk[] load = new VoxelChunk[maxNumThreads()];
                mut.WaitOne();
                for (int i = 0; i < loadedReloads.Count; i++)
                {
                    if (!loadedReloads[i].loading && loadedReloads[i].graphicsLoaded)
                    {
                            VoxelChunk c = loadedReloads[i];
                        for (int ii = 0; ii < load.Length; ii++)
                        {
                            if (load[ii] == null)
                            {
                                load[ii] = c;
                                ii = load.Length;
                            }
                            else
                            if ((c.pos - VoxelWorld.pos).magnitude < (load[ii].pos - VoxelWorld.pos).magnitude)
                            {
                                VoxelChunk cc = c;
                                c = load[ii];
                                load[ii] = cc;
                            }
                        }
                    }
                    else
                    {
                    }
                }
                mut.ReleaseMutex();
                for (int i = 0; i < load.Length; i++)
                {
                    if (load[i] != null)
                    {
                        load[i].RunReloadChunk();
                    }
                }
            }
            {
                for (int i = 0; i < loadedVoxelChunks.Count; i++)
                {
                    if (loadedVoxelChunks[i].shouldMove() && loadedVoxelChunks[i].isCanMove())
                    {
                        VoxelChunk c = loadedVoxelChunks[i];
                        c.Unload();
                        int index1 = c.index1;
                        int index2 = c.index2;
                        int index3 = c.index3;
                        index1 = c.pos.x - VoxelWorld.pos.x > size * VoxelWorld.loadSize/2 + size ? index1 - VoxelWorld.loadSize : c.pos.x - VoxelWorld.pos.x < -(size * VoxelWorld.loadSize / 2 + size) ? index1 + VoxelWorld.loadSize : index1;
                        index2 = c.pos.z - VoxelWorld.pos.z > size * VoxelWorld.loadSize / 2 + size ? index2 - VoxelWorld.loadSize : c.pos.z - VoxelWorld.pos.z < -(size * VoxelWorld.loadSize / 2 + size) ? index2 + VoxelWorld.loadSize : index2;
                        index3 = c.pos.y - VoxelWorld.pos.y > size * VoxelWorld.loadHeight / 2 + size ? index3 - VoxelWorld.loadHeight : c.pos.y - VoxelWorld.pos.y < -(size * VoxelWorld.loadHeight / 2 + size) ? index3 + VoxelWorld.loadHeight : index3;
                        c.LoadChunk(index1, index2, index3);
                    }
                }
            }
            {

                VoxelChunk[] load = new VoxelChunk[maxNumThreads() - numThreads];
                for (int i = 0; i < loadedVoxels.Count; i++)
                {
                    if (!loadedVoxels[i].loaded && loadedVoxels[i].isVoxelsLoaded() && IntersectsBox(new Bounds(new Vector3(loadedVoxels[i].index1 * size, 0, loadedVoxels[i].index2 * size), new Vector3(16, 1000, 16)), 0))
                    {
                        VoxelChunk c = loadedVoxels[i];
                        for (int ii = 0; ii < load.Length; ii++)
                        {
                            if (load[ii] == null)
                            {
                                load[ii] = c;
                                ii = load.Length;
                            }
                            else
                            if ((c.pos - VoxelWorld.pos).magnitude < (load[ii].pos - VoxelWorld.pos).magnitude)
                            {
                                VoxelChunk cc = c;
                                c = load[ii];
                                load[ii] = cc;
                            }
                        }
                    }
                }
                for (int i = 0; i < load.Length; i++)
                {
                    if (load[i] != null)
                    {
                        load[i].RunLoad();
                    }
                }
            }
            {

                VoxelChunk[] load = new VoxelChunk[maxNumThreads() - numThreads];
                for (int i = 0; i < loadedChunks.Count; i++)
                {
                    if (!loadedChunks[i].voxelsLoaded && IntersectsBox(new Bounds(new Vector3(loadedChunks[i].index1 * size, 0, loadedChunks[i].index2 * size), new Vector3(16, 1000, 16)), Mathf.Sqrt(size*size+size*size+size*size)))
                    {
                        VoxelChunk c = loadedChunks[i];
                        for (int ii = 0; ii < load.Length; ii++)
                        {
                            if (load[ii] == null)
                            {
                                load[ii] = c;
                                ii = load.Length;
                            }
                            else
                            if ((c.pos - VoxelWorld.pos).magnitude < (load[ii].pos - VoxelWorld.pos).magnitude)
                            {
                                VoxelChunk cc = c;
                                c = load[ii];
                                load[ii] = cc;
                            }
                        }
                    }
                }
                for (int i = 0; i < load.Length; i++)
                {
                    if (load[i] != null)
                    {
                        load[i].RunLoadChunk();
                    }
                }
            }
        }
        //Done = true;
        //numThreads--;
    }
    public void Unload()
    {
        voxelsLoaded = false;
        chunksLoaded = false;
        voxelsLoadedAround = false;
        chunksLoadedAround = false;
        loading = false;
        loaded = false;
        smoothed = false;
        graphicsLoaded = false;
        reloading = false;
        hasGraphics = false;
        vertices.Clear();
        indices.Clear();
        normals.Clear();
        colors.Clear(); 
        EdgeVertices.Clear();
        loadedChunks.Remove(this);
        loadedVoxels.Remove(this);
        loadedLoads.Remove(this);
        loadedSmooths.Remove(this);
        loadedReloads.Remove(this);

    }

    public void LoadGraphics()
    {
        mf.mesh.Clear();
        mf.mesh.vertices = vertices.ToArray();
        mf.mesh.colors = colors.ToArray();
        mf.mesh.triangles = indices.ToArray();
        mf.mesh.normals = normals.ToArray();
        mf.mesh.UploadMeshData(false);
        mf.mesh.RecalculateBounds();
        mc.sharedMesh = mf.mesh;
        transform.position = pos;
        graphicsLoaded = true;
        reloading = false;
    }

    public bool isVoxelsLoaded()
    {
        bool should = false;
        for (int i = 1; i < 3; i++)
        {
            for (int ii = 1; ii < 3; ii++)
            {
                for (int iii = 1; iii < 3; iii++)
                {
                    VoxelChunk c = chunks[i, ii, iii];
                    if (c == null)
                    {
                        return false;
                    }else 
                    if (!c.voxelsLoaded)
                    {
                        return false;
                    }
                    else if ((c.index1 - index1) * (c.index1 - index1) > 1 | (c.index2 - index2) * (c.index2 - index2) > 1 | (c.index3 - index3) * (c.index3 - index3) > 1)
                    {
                        return false;
                    }
                }
            }
        }
        for (int i = 0; i < 3; i++)
        {
            for (int ii = 0; ii < 3; ii++)
            {
                for (int iii = 0; iii < 3; iii++)
                {
                    if (chunks[i, ii, iii] != null && chunks[i, ii, iii].hasGraphics)
                    {
                        should = true;
                    }
                }
            }
        }

        if (should)
        {
            return true;
        }else
        {
            return false;
        }
    }
    bool shouldMove()
    {
        if (Mathf.Abs(pos.x - VoxelWorld.pos.x) > size * VoxelWorld.loadSize / 2 + size | Mathf.Abs(pos.y - VoxelWorld.pos.y) > size * VoxelWorld.loadHeight/2+size | Mathf.Abs(pos.z - VoxelWorld.pos.z) > size * VoxelWorld.loadSize / 2 + size)
        {
            return true;
        }
        return false;
    }
    public bool isCanMove()
    {
        for (int i = 0; i < 3; i++)
        {
            for (int ii = 0; ii < 3; ii++)
            {
                for (int iii = 0; iii < 3; iii++)
                {
                    VoxelChunk c = chunks[i, ii, iii];
                    if (c == null)
                    {
                        return false;
                    }
                    else
                    if (c.loading)
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }
    public bool isChunkLoaded()
    {
        for (int i = 0; i < 3; i++)
        {
            for (int ii = 0; ii < 3; ii++)
            {
                for (int iii = 0; iii < 3; iii++)
                {
                    VoxelChunk c = chunks[i, ii, iii];
                    if (c == null)
                    {
                        return false;
                    }
                    else
                    if (!c.loaded)
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }

    public void RunLoadChunk()
    {
        if (numThreads < maxNumThreads())
        {
        }
            mut.WaitOne();
            Thread thread = new Thread(LoadVoxels);
            loading = true;
            numThreads++;
            loadedChunks.Remove(this);
            loadedVoxels.Add(this);
            thread.Start();
            mut.ReleaseMutex();
    }

    public void RunLoad()
    {
        if (numThreads < maxNumThreads())
        {
            mut.WaitOne();
            Thread thread = new Thread(Load);
            loading = true;
            numThreads++;
            loadedVoxels.Remove(this);
            loadedSmooths.Add(this);
            thread.Start();
            mut.ReleaseMutex();
        }
    }
    public void RunSmooth()
    {
        if (numThreads < maxNumThreads())
        {
            mut.WaitOne();
            Thread thread = new Thread(Smooth);
            loading = true;
            numThreads++;
            loadedLoads.Remove(this);
            loadedSmooths.Add(this);
            thread.Start();
            mut.ReleaseMutex();
        }
    }
    public void RunReloadChunk()
    {
        if (numThreads < maxNumThreads())
        {
            mut.WaitOne();
            Thread thread = new Thread(ReloadChunk);
            loading = true;
            numThreads++;
            loadedReloads.Remove(this);
            thread.Start();
            mut.ReleaseMutex();
        }
    }

    public void Destroy()
    {

    }

    public void loadChunks(VoxelChunk[,,] chunks)
    {
        this.chunks = chunks;
        chunksLoaded = true;
    }

    public void LoadChunk(int index1, int index2, int index3)
    {
        this.index1 = index1;
        this.index2 = index2;
        this.index3 = index3;
        pos = new Vector3(index1 * size, index3 * size, index2 * size);
        loadedChunks.Add(this);
        if (!loadedVoxelChunks.Contains(this))
        {
            loadedVoxelChunks.Add(this);
        }
    }

    public void LoadVoxels()
    {
        float c = -1;
        for (int i = 0; i < size; i++)
        {
            for (int ii = 0; ii < size; ii++)
            {
                float v = GetHeight(index1 * size + i, index2 * size + ii);
                for (int iii = 0; iii < size; iii++)
                {
                    if (index3 * size + iii < v)
                    {
                        if (v > 0)
                        {
                            types[i, ii, iii] = v - (index3 * size + iii) < 2  ? 0 : 1;
                        } else
                        {
                            types[i, ii, iii] = 1;
                        }
                        densities[i, ii, iii] = v - (index3 * size + iii) > 1 ? 1 : (v - (index3 * size + iii));
                    }
                    else
                    {
                        types[i, ii, iii] = 1;
                        densities[i, ii, iii] = 0;
                    }
                    if (c == -1)
                    {
                        c = densities[i, ii, iii];
                    } else if (hasGraphics == false && (c > 0 && densities[i, ii, iii] == 0) || (c == 0 && densities[i, ii, iii] > 0))
                    {
                        hasGraphics = true;
                    }
                }
            }
        }
        mut.WaitOne();
        numThreads--;
        loading = false;
        voxelsLoaded = true;
        mut.ReleaseMutex();
    }

    float GetDensity(int i, int ii, int iii)
    {
        int ier = 1;
        if (i >= size)
        {
            i -= size;
            ier++;
        }else if (i < 0)
        {
            i += size;
            ier--;
        }
        int iier = 1;
        if (ii >= size)
        {
            ii -= size;
            iier++;
        }
        else if (ii < 0)
        {
            ii += size;
            iier--;
        }
        int iiier = 1;
        if (iii >= size)
        {
            iii -= size;
            iiier++;
        }
        else if (iii < 0)
        {
            iii += size;
            iiier--;
        }
        return chunks[ier,iier,iiier].densities[i,ii,iii];
    }
    
    int GetType(int i, int ii, int iii)
    {
        int ier = 1;
        if (i >= size)
        {
            i -= size;
            ier++;
        }
        else if (i < 0)
        {
            i += size;
            ier--;
        }
        int iier = 1;
        if (ii >= size)
        {
            ii -= size;
            iier++;
        }
        else if (ii < 0)
        {
            ii += size;
            iier--;
        }
        int iiier = 1;
        if (iii >= size)
        {
            iii -= size;
            iiier++;
        }
        else if (iii < 0)
        {
            iii += size;
            iiier--;
        }
        return chunks[ier, iier, iiier].types[i, ii, iii];
    }
    void SetDensity(int i, int ii, int iii, float density)
    {
        int ier = 1;
        if (i >= size)
        {
            i -= size;
            ier++;
        }
        else if (i < 0)
        {
            i += size;
            ier--;
        }
        int iier = 1;
        if (ii >= size)
        {
            ii -= size;
            iier++;
        }
        else if (ii < 0)
        {
            ii += size;
            iier--;
        }
        int iiier = 1;
        if (iii >= size)
        {
            iii -= size;
            iiier++;
        }
        else if (iii < 0)
        {
            iii += size;
            iiier--;
        }
        chunks[ier, iier, iiier].densities[i, ii, iii] = density;
    }

    void SetType(int i, int ii, int iii, int type)
    {
        int ier = 1;
        if (i >= size)
        {
            i -= size;
            ier++;
        }
        else if (i < 0)
        {
            i += size;
            ier--;
        }
        int iier = 1;
        if (ii >= size)
        {
            ii -= size;
            iier++;
        }
        else if (ii < 0)
        {
            ii += size;
            iier--;
        }
        int iiier = 1;
        if (iii >= size)
        {
            iii -= size;
            iiier++;
        }
        else if (iii < 0)
        {
            iii += size;
            iiier--;
        }
        chunks[ier, iier, iiier].types[i, ii, iii] = type;
    }

    void ReloadChunk()
    {
        vertices.Clear();
        colors.Clear();
        indices.Clear();
        normals.Clear();
        EdgeVertices.Clear();
        int sizer = 0;
        int sizerer = 0;
        for (int i = 0; i < size; i++)
        {
            for (int ii = 0; ii < size; ii++)
            {
                for (int iii = 0; iii < size; iii++)
                {
                    LoadCube(i, ii, iii, sizer);
                }
            }
            sizer = sizerer;
            sizerer = vertices.Count;
        }
        for (int i = 0; i < indices.Count; i += 3)
        {
            Vector3 U = vertices[indices[i]] - vertices[indices[i + 1]];
            Vector3 V = vertices[indices[i]] - vertices[indices[i + 2]];
            Vector3 normal = new Vector3(U.y * V.z - U.z * V.y, U.z * V.x - U.x * V.z, U.x * V.y - U.y * V.x);
            normals[indices[i]] += normal;
            normals[indices[i + 1]] += normal;
            normals[indices[i + 2]] += normal;
        }
        mut.WaitOne();
        loading = false;
        numThreads--;
        graphicsLoaded = false;
        mut.ReleaseMutex();
    }

    public void Load()
    {
        /*for (int i = 0; i < size / 2; i++)
        {
            types[size / 2, size / 2, size / 2 + i] = 1;
            densities[size / 2, size / 2, size / 2 - 4 + i] = (1.0f - (float)i / (size / 2)) / 2.0f + 0.5f;
        }*/
        int sizer = 0;
        int sizerer = 0;
        for (int i = 0; i < size; i++)
        {
            for (int ii = 0; ii < size; ii++)
            {
                for (int iii = 0; iii < size; iii++)
                {
                    LoadCube(i, ii, iii, sizer);
                }
            }
            sizer = sizerer;
            sizerer = vertices.Count;
        }
        for (int i = 0; i < indices.Count; i += 3)
        {
            Vector3 U = vertices[indices[i]] - vertices[indices[i + 1]];
            Vector3 V = vertices[indices[i]] - vertices[indices[i + 2]];
            Vector3 normal = new Vector3(U.y * V.z - U.z * V.y, U.z * V.x - U.x * V.z, U.x * V.y - U.y * V.x);
            normals[indices[i]] += normal;
            normals[indices[i + 1]] += normal;
            normals[indices[i + 2]] += normal;
        }
        for (int i = 0; i < EdgeVertices.Count; i++)
        {
            Vector3 v = pos + vertices[EdgeVertices[i]];
            Vector3 n = normals[EdgeVertices[i]];
            for (int o = 0; o < 3; o++)
            {
                for (int oo = 0; oo < 3; oo++)
                {
                    for (int ooo = 0; ooo < 3; ooo++)
                    {
                        if (!(o == 1 && oo == 1 && ooo == 1))
                        {
                            VoxelChunk c = chunks[o, oo, ooo];
                            if (c.loaded)
                            {
                                for (int ii = 0; ii < c.EdgeVertices.Count; ii++)
                                {
                                    if (c.pos + c.vertices[c.EdgeVertices[ii]] == v)
                                    {
                                        n += c.normals[c.EdgeVertices[ii]];
                                    }
                                }
                            }
                        }
                    }
                }
            }
            n.Normalize();
            normals[EdgeVertices[i]] = n;
            for (int o = 0; o < 3; o++)
            {
                for (int oo = 0; oo < 3; oo++)
                {
                    for (int ooo = 0; ooo < 3; ooo++)
                    {
                        if (!(o == 1 && oo == 1 && ooo == 1))
                        {
                            VoxelChunk c = chunks[o, oo, ooo];
                            if (c.loaded)
                            {
                                for (int ii = 0; ii < c.EdgeVertices.Count; ii++)
                                {
                                    if (c.pos + c.vertices[c.EdgeVertices[ii]] == v)
                                    {
                                        c.normals[c.EdgeVertices[ii]] = n;
                                    }
                                }
                                mut.WaitOne();
                                c.graphicsLoaded = false;
                                mut.ReleaseMutex();
                            }
                        }
                    }
                }
            }
        }
        mut.WaitOne();
        loading = false;
        numThreads--;
        loaded = true;
        mut.ReleaseMutex();
    }

    void LoadCube(int i, int ii, int iii, int sizer)
    {
        // 8 corners of the current cube
        float[] cubeCorners = new float[8]
        {
                        GetDensity(i,ii,iii),
                        GetDensity(i+1,ii,iii),
                        GetDensity(i+1,ii+1,iii),
                        GetDensity(i,ii+1,iii),
                        GetDensity(i,ii,iii+1),
                        GetDensity(i+1,ii,iii+1),
                        GetDensity(i+1,ii+1,iii+1),
                        GetDensity(i,ii+1,iii+1)
        };
        float[] cubeCornersT = new float[8]
        {
                        GetType(i,ii,iii),
                        GetType(i+1,ii,iii),
                        GetType(i+1,ii+1,iii),
                        GetType(i,ii+1,iii),
                        GetType(i,ii,iii+1),
                        GetType(i+1,ii,iii+1),
                        GetType(i+1,ii+1,iii+1),
                        GetType(i,ii+1,iii+1)
        };
        Vector3[] cubeCornersV = new Vector3[8]
        {
                        new Vector3(i,iii,ii),
                        new Vector3(i+1,iii,ii),
                        new Vector3(i+1,iii,ii+1),
                        new Vector3(i,iii,ii+1),
                        new Vector3(i,iii+1,ii),
                        new Vector3(i+1,iii+1,ii),
                        new Vector3(i+1,iii+1,ii+1),
                        new Vector3(i,iii+1,ii+1)
        };
        /*{
points[indexFromCoord(id.x, id.y, id.z)],
points[indexFromCoord(id.x + 1, id.y, id.z)],
points[indexFromCoord(id.x + 1, id.y, id.z + 1)],
points[indexFromCoord(id.x, id.y, id.z + 1)],
points[indexFromCoord(id.x, id.y + 1, id.z)],
points[indexFromCoord(id.x + 1, id.y + 1, id.z)],
points[indexFromCoord(id.x + 1, id.y + 1, id.z + 1)],
points[indexFromCoord(id.x, id.y + 1, id.z + 1)]*/

        // Calculate unique index for each cube configuration.
        // There are 256 possible values
        // A value of 0 means cube is entirely inside surface; 255 entirely outside.
        // The value is used to look up the edge table, which indicates which edges of the cube are cut by the isosurface.
        int cubeIndex = 0;
        if (cubeCorners[0] < isoLevel) cubeIndex |= 1;
        if (cubeCorners[1] < isoLevel) cubeIndex |= 2;
        if (cubeCorners[2] < isoLevel) cubeIndex |= 4;
        if (cubeCorners[3] < isoLevel) cubeIndex |= 8;
        if (cubeCorners[4] < isoLevel) cubeIndex |= 16;
        if (cubeCorners[5] < isoLevel) cubeIndex |= 32;
        if (cubeCorners[6] < isoLevel) cubeIndex |= 64;
        if (cubeCorners[7] < isoLevel) cubeIndex |= 128;
        // Create triangles for current cube configuration
        bool edge = (i == 0 | i == size - 1 | ii == 0 | ii == size - 1 | iii == 0 | iii == size - 1);
        for (int ier = 0; triangulation[cubeIndex, ier] != -1; ier += 3)
        {
            // Get indices of corner points A and B for each of the three edges
            // of the cube that need to be joined to form the triangle.
            int a0 = cornerIndexAFromEdge[triangulation[cubeIndex, ier]];
            int b0 = cornerIndexBFromEdge[triangulation[cubeIndex, ier]];

            int a1 = cornerIndexAFromEdge[triangulation[cubeIndex, ier + 1]];
            int b1 = cornerIndexBFromEdge[triangulation[cubeIndex, ier + 1]];

            int a2 = cornerIndexAFromEdge[triangulation[cubeIndex, ier + 2]];
            int b2 = cornerIndexBFromEdge[triangulation[cubeIndex, ier + 2]];
            edge = (cubeCornersV[b0].x == 0 | cubeCornersV[b0].x == size - 1 | cubeCornersV[b0].y == 0 | cubeCornersV[b0].y == size - 1 | cubeCornersV[b0].z == 0 | cubeCornersV[b0].z == size - 1);
            AddVertex(interpolateVerts(cubeCorners[a0], cubeCorners[b0], cubeCornersV[a0], cubeCornersV[b0]), (cubeCornersV[b0].x == 0 | cubeCornersV[b0].x == size | cubeCornersV[b0].y == 0 | cubeCornersV[b0].y == size | cubeCornersV[b0].z == 0 | cubeCornersV[b0].z == size), cubeCornersT[a0], i, ii, iii, sizer);

            edge = (cubeCornersV[b2].x == 0 | cubeCornersV[b2].x == size - 1 | cubeCornersV[b2].y == 0 | cubeCornersV[b2].y == size - 1 | cubeCornersV[b2].z == 0 | cubeCornersV[b2].z == size - 1);
            AddVertex(interpolateVerts(cubeCorners[a2], cubeCorners[b2], cubeCornersV[a2], cubeCornersV[b2]), (cubeCornersV[b2].x == 0 | cubeCornersV[b2].x == size | cubeCornersV[b2].y == 0 | cubeCornersV[b2].y == size | cubeCornersV[b2].z == 0 | cubeCornersV[b2].z == size), cubeCornersT[a2], i, ii, iii, sizer);

            edge = (cubeCornersV[b1].x == 0 | cubeCornersV[b1].x == size - 1 | cubeCornersV[b1].y == 0 | cubeCornersV[b1].y == size - 1 | cubeCornersV[b1].z == 0 | cubeCornersV[b1].z == size - 1);
            AddVertex(interpolateVerts(cubeCorners[a1], cubeCorners[b1], cubeCornersV[a1], cubeCornersV[b1]), (cubeCornersV[b1].x == 0 | cubeCornersV[b1].x == size | cubeCornersV[b1].y == 0 | cubeCornersV[b1].y == size | cubeCornersV[b1].z == 0 | cubeCornersV[b1].z == size), cubeCornersT[a1], i, ii, iii, sizer);

        }
    }

    List<int> EdgeVertices = new List<int>();

    void AddVertex(Vector3 v, bool edge, float r, int i, int ii, int iii, int sizer)
    {
        for (int ier = sizer; ier < vertices.Count; ier++)
        {
            if ((vertices[ier]-v).magnitude == 0)
            {
                indices.Add(ier);
                return;
            }
        }
        indices.Add(vertices.Count);
        if (edge)
        {
            EdgeVertices.Add(vertices.Count);
        }
        colors.Add(new Color(r, i, ii, iii));
        vertices.Add(v);
        normals.Add(new Vector3());
    }

    public void Smooth()
    {
        for (int i = 0; i < EdgeVertices.Count; i++)
        {
            Vector3 v = pos + vertices[EdgeVertices[i]];
            Vector3 n = normals[EdgeVertices[i]];
            for (int o = 0; o < 3; o++)
            {
                for (int oo = 0; oo < 3; oo++)
                {
                    for (int ooo = 0; ooo < 3; ooo++)
                    {
                        if (!(o == 1 && oo == 1 && ooo == 1))
                        {
                            VoxelChunk c = chunks[o, oo, ooo];
                            for (int ii = 0; ii < c.EdgeVertices.Count; ii++)
                            {
                                if (c.pos + c.vertices[c.EdgeVertices[ii]] == v)
                                {
                                    n += c.normals[c.EdgeVertices[ii]];
                                }
                            }
                        }
                    }
                }
            }
            n.Normalize();
            normals[EdgeVertices[i]] = n;
            for (int o = 0; o < 3; o++)
            {
                for (int oo = 0; oo < 3; oo++)
                {
                    for (int ooo = 0; ooo < 3; ooo++)
                    {
                        if (!(o == 1 && oo == 1 && ooo == 1))
                        {
                            VoxelChunk c = chunks[o, oo, ooo];
                            for (int ii = 0; ii < c.EdgeVertices.Count; ii++)
                            {
                                if (c.pos + c.vertices[c.EdgeVertices[ii]] == v)
                                {
                                    c.normals[c.EdgeVertices[ii]] = n;
                                }
                            }
                        }
                    }
                }
            }
        }
        mut.WaitOne();
        loading = false;
        numThreads--;
        smoothed = true;
        mut.ReleaseMutex();
    }

    public void Place(Vector3 hit, float s)
    {
        bool b = true;
        for (int i = 0; i < 3; i++)
        {
            for (int ii = 0; ii < 3; ii++)
            {
                for (int iii = 0; iii < 3; iii++)
                {
                    if (chunks[i,ii,iii].reloading)
                    {
                        b = false;
                    }
                }
            }
        }
        if (b)
        {
            hit -= transform.position;
            UnityEngine.Debug.Log(hit);
            int sizer = Mathf.CeilToInt(s);
            int x = Mathf.FloorToInt(hit.x);
            int y = Mathf.FloorToInt(hit.y);
            int z = Mathf.FloorToInt(hit.z);
            for (int i = 0; i < sizer*2+2; i++)
            {
                for (int ii = 0; ii < sizer*2+2; ii++)
                {
                    for (int iii = 0; iii < sizer*2+2; iii++)
                    {
                        float d = GetDensity(x - sizer + i, z - sizer + ii, y - sizer + iii);
                        d += 1.0f - (hit - new Vector3(x - sizer + i, y - sizer + iii, z - sizer + ii)).magnitude / s < 0 ? 0 : 1.0f - (hit - new Vector3(x - sizer + i, y - sizer + iii, z - sizer + ii)).magnitude / s;
                        d = d > 1 ? 1 : d;
                        SetDensity(x - sizer + i, z - sizer + ii, y - sizer + iii, d);
                    }
                }
            }
            for (int i = 0; i < 3; i++)
            {
                for (int ii = 0; ii < 3; ii++)
                {
                    for (int iii = 0; iii < 3; iii++)
                    {
                        if (chunks[i, ii, iii] != null && !loadedReloads.Contains(chunks[i, ii, iii]))
                        {
                            chunks[i, ii, iii].hasGraphics = true;
                            chunks[i, ii, iii].reloading = true;
                            loadedReloads.Add(chunks[i, ii, iii]);
                        }
                    }
                }
            }
        }
    }
    public void Break(Vector3 hit, float s)
    {
        bool b = true;
        for (int i = 0; i < 3; i++)
        {
            for (int ii = 0; ii < 3; ii++)
            {
                for (int iii = 0; iii < 3; iii++)
                {
                    if (chunks[i, ii, iii].reloading)
                    {
                        b = false;
                    }
                }
            }
        }
        if (b)
        {
            hit -= transform.position;
            int sizer = Mathf.CeilToInt(s);
            int x = Mathf.FloorToInt(hit.x);
            int y = Mathf.FloorToInt(hit.y);
            int z = Mathf.FloorToInt(hit.z);
            for (int i = 0; i < sizer * 2 + 2; i++)
            {
                for (int ii = 0; ii < sizer * 2 + 2; ii++)
                {
                    for (int iii = 0; iii < sizer * 2 + 2; iii++)
                    {
                        float d = GetDensity(x - sizer + i, z - sizer + ii, y - sizer + iii);
                        d -= 1.0f - (hit - new Vector3(x - sizer + i, y - sizer + iii, z - sizer + ii)).magnitude / s < 0 ? 0 : 1.0f - (hit - new Vector3(x - sizer + i, y - sizer + iii, z - sizer + ii)).magnitude / s;
                        d = d < 0 ? 0 : d;
                        SetDensity(x - sizer + i, z - sizer + ii, y - sizer + iii, d);
                    }
                }
            }
            for (int i = 0; i < 3; i++)
            {
                for (int ii = 0; ii < 3; ii++)
                {
                    for (int iii = 0; iii < 3; iii++)
                    {
                        if (chunks[i, ii, iii] != null && !loadedReloads.Contains(chunks[i, ii, iii]))
                        {
                            chunks[i, ii, iii].hasGraphics = true;
                            chunks[i, ii, iii].reloading = true;
                            loadedReloads.Add(chunks[i, ii, iii]);
                        }
                    }
                }
            }
        }
    }

    float GetHeight(int x, int z)
    {
        double height = 0;
        double amount = 1;
        double max = 0;
        for (int i = 0; i < 8; i++)
        {
            height += worldNoise.ValueCoherentNoise3D(x / amount, z / amount, i, 0)*amount;
            max += amount;
            amount *= 2;
        }
        height /= max;
        height *= height < 0 ? 0.1 : height * height;
        height /= 4;
        height *= max;
        return (float)height;
    }

    float isoLevel = 0.5f;

    Vector3 interpolateVerts(float f1, float f2, Vector3 v1, Vector3 v2)
    {
        float t = (isoLevel - f1) / (f2 - f1);
        return v1 + t * (v2 - v1);
    }

    int indexFromCoord(int x, int y, int z)
    {
        return z * size * size + y * size + x;
    }

    // Update is called once per frame
    void Update()
    {
    }
    static int[] edges = new int[256]{
    0x0,
    0x109,
    0x203,
    0x30a,
    0x406,
    0x50f,
    0x605,
    0x70c,
    0x80c,
    0x905,
    0xa0f,
    0xb06,
    0xc0a,
    0xd03,
    0xe09,
    0xf00,
    0x190,
    0x99,
    0x393,
    0x29a,
    0x596,
    0x49f,
    0x795,
    0x69c,
    0x99c,
    0x895,
    0xb9f,
    0xa96,
    0xd9a,
    0xc93,
    0xf99,
    0xe90,
    0x230,
    0x339,
    0x33,
    0x13a,
    0x636,
    0x73f,
    0x435,
    0x53c,
    0xa3c,
    0xb35,
    0x83f,
    0x936,
    0xe3a,
    0xf33,
    0xc39,
    0xd30,
    0x3a0,
    0x2a9,
    0x1a3,
    0xaa,
    0x7a6,
    0x6af,
    0x5a5,
    0x4ac,
    0xbac,
    0xaa5,
    0x9af,
    0x8a6,
    0xfaa,
    0xea3,
    0xda9,
    0xca0,
    0x460,
    0x569,
    0x663,
    0x76a,
    0x66,
    0x16f,
    0x265,
    0x36c,
    0xc6c,
    0xd65,
    0xe6f,
    0xf66,
    0x86a,
    0x963,
    0xa69,
    0xb60,
    0x5f0,
    0x4f9,
    0x7f3,
    0x6fa,
    0x1f6,
    0xff,
    0x3f5,
    0x2fc,
    0xdfc,
    0xcf5,
    0xfff,
    0xef6,
    0x9fa,
    0x8f3,
    0xbf9,
    0xaf0,
    0x650,
    0x759,
    0x453,
    0x55a,
    0x256,
    0x35f,
    0x55,
    0x15c,
    0xe5c,
    0xf55,
    0xc5f,
    0xd56,
    0xa5a,
    0xb53,
    0x859,
    0x950,
    0x7c0,
    0x6c9,
    0x5c3,
    0x4ca,
    0x3c6,
    0x2cf,
    0x1c5,
    0xcc,
    0xfcc,
    0xec5,
    0xdcf,
    0xcc6,
    0xbca,
    0xac3,
    0x9c9,
    0x8c0,
    0x8c0,
    0x9c9,
    0xac3,
    0xbca,
    0xcc6,
    0xdcf,
    0xec5,
    0xfcc,
    0xcc,
    0x1c5,
    0x2cf,
    0x3c6,
    0x4ca,
    0x5c3,
    0x6c9,
    0x7c0,
    0x950,
    0x859,
    0xb53,
    0xa5a,
    0xd56,
    0xc5f,
    0xf55,
    0xe5c,
    0x15c,
    0x55,
    0x35f,
    0x256,
    0x55a,
    0x453,
    0x759,
    0x650,
    0xaf0,
    0xbf9,
    0x8f3,
    0x9fa,
    0xef6,
    0xfff,
    0xcf5,
    0xdfc,
    0x2fc,
    0x3f5,
    0xff,
    0x1f6,
    0x6fa,
    0x7f3,
    0x4f9,
    0x5f0,
    0xb60,
    0xa69,
    0x963,
    0x86a,
    0xf66,
    0xe6f,
    0xd65,
    0xc6c,
    0x36c,
    0x265,
    0x16f,
    0x66,
    0x76a,
    0x663,
    0x569,
    0x460,
    0xca0,
    0xda9,
    0xea3,
    0xfaa,
    0x8a6,
    0x9af,
    0xaa5,
    0xbac,
    0x4ac,
    0x5a5,
    0x6af,
    0x7a6,
    0xaa,
    0x1a3,
    0x2a9,
    0x3a0,
    0xd30,
    0xc39,
    0xf33,
    0xe3a,
    0x936,
    0x83f,
    0xb35,
    0xa3c,
    0x53c,
    0x435,
    0x73f,
    0x636,
    0x13a,
    0x33,
    0x339,
    0x230,
    0xe90,
    0xf99,
    0xc93,
    0xd9a,
    0xa96,
    0xb9f,
    0x895,
    0x99c,
    0x69c,
    0x795,
    0x49f,
    0x596,
    0x29a,
    0x393,
    0x99,
    0x190,
    0xf00,
    0xe09,
    0xd03,
    0xc0a,
    0xb06,
    0xa0f,
    0x905,
    0x80c,
    0x70c,
    0x605,
    0x50f,
    0x406,
    0x30a,
    0x203,
    0x109,
    0x0
};

    static int[,] triangulation = new int[256, 16] {
    {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 1, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 8, 3, 9, 8, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 8, 3, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 2, 10, 0, 2, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 2, 8, 3, 2, 10, 8, 10, 9, 8, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 11, 2, 8, 11, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 9, 0, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 11, 2, 1, 9, 11, 9, 8, 11, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 10, 1, 11, 10, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 10, 1, 0, 8, 10, 8, 11, 10, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 9, 0, 3, 11, 9, 11, 10, 9, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 4, 3, 0, 7, 3, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 1, 9, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 4, 1, 9, 4, 7, 1, 7, 3, 1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 2, 10, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 4, 7, 3, 0, 4, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 2, 10, 9, 0, 2, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1 },
    { 2, 10, 9, 2, 9, 7, 2, 7, 3, 7, 9, 4, -1, -1, -1, -1 },
    { 8, 4, 7, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 11, 4, 7, 11, 2, 4, 2, 0, 4, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 0, 1, 8, 4, 7, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1 },
    { 4, 7, 11, 9, 4, 11, 9, 11, 2, 9, 2, 1, -1, -1, -1, -1 },
    { 3, 10, 1, 3, 11, 10, 7, 8, 4, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 11, 10, 1, 4, 11, 1, 0, 4, 7, 11, 4, -1, -1, -1, -1 },
    { 4, 7, 8, 9, 0, 11, 9, 11, 10, 11, 0, 3, -1, -1, -1, -1 },
    { 4, 7, 11, 4, 11, 9, 9, 11, 10, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 5, 4, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 5, 4, 1, 5, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 8, 5, 4, 8, 3, 5, 3, 1, 5, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 2, 10, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 0, 8, 1, 2, 10, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1 },
    { 5, 2, 10, 5, 4, 2, 4, 0, 2, -1, -1, -1, -1, -1, -1, -1 },
    { 2, 10, 5, 3, 2, 5, 3, 5, 4, 3, 4, 8, -1, -1, -1, -1 },
    { 9, 5, 4, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 11, 2, 0, 8, 11, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 5, 4, 0, 1, 5, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1 },
    { 2, 1, 5, 2, 5, 8, 2, 8, 11, 4, 8, 5, -1, -1, -1, -1 },
    { 10, 3, 11, 10, 1, 3, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1 },
    { 4, 9, 5, 0, 8, 1, 8, 10, 1, 8, 11, 10, -1, -1, -1, -1 },
    { 5, 4, 0, 5, 0, 11, 5, 11, 10, 11, 0, 3, -1, -1, -1, -1 },
    { 5, 4, 8, 5, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 7, 8, 5, 7, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 3, 0, 9, 5, 3, 5, 7, 3, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 7, 8, 0, 1, 7, 1, 5, 7, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 7, 8, 9, 5, 7, 10, 1, 2, -1, -1, -1, -1, -1, -1, -1 },
    { 10, 1, 2, 9, 5, 0, 5, 3, 0, 5, 7, 3, -1, -1, -1, -1 },
    { 8, 0, 2, 8, 2, 5, 8, 5, 7, 10, 5, 2, -1, -1, -1, -1 },
    { 2, 10, 5, 2, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1 },
    { 7, 9, 5, 7, 8, 9, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 5, 7, 9, 7, 2, 9, 2, 0, 2, 7, 11, -1, -1, -1, -1 },
    { 2, 3, 11, 0, 1, 8, 1, 7, 8, 1, 5, 7, -1, -1, -1, -1 },
    { 11, 2, 1, 11, 1, 7, 7, 1, 5, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 5, 8, 8, 5, 7, 10, 1, 3, 10, 3, 11, -1, -1, -1, -1 },
    { 5, 7, 0, 5, 0, 9, 7, 11, 0, 1, 0, 10, 11, 10, 0, -1 },
    { 11, 10, 0, 11, 0, 3, 10, 5, 0, 8, 0, 7, 5, 7, 0, -1 },
    { 11, 10, 5, 7, 11, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 8, 3, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 0, 1, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 8, 3, 1, 9, 8, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 6, 5, 2, 6, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 6, 5, 1, 2, 6, 3, 0, 8, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 6, 5, 9, 0, 6, 0, 2, 6, -1, -1, -1, -1, -1, -1, -1 },
    { 5, 9, 8, 5, 8, 2, 5, 2, 6, 3, 2, 8, -1, -1, -1, -1 },
    { 2, 3, 11, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 11, 0, 8, 11, 2, 0, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 1, 9, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1 },
    { 5, 10, 6, 1, 9, 2, 9, 11, 2, 9, 8, 11, -1, -1, -1, -1 },
    { 6, 3, 11, 6, 5, 3, 5, 1, 3, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 8, 11, 0, 11, 5, 0, 5, 1, 5, 11, 6, -1, -1, -1, -1 },
    { 3, 11, 6, 0, 3, 6, 0, 6, 5, 0, 5, 9, -1, -1, -1, -1 },
    { 6, 5, 9, 6, 9, 11, 11, 9, 8, -1, -1, -1, -1, -1, -1, -1 },
    { 5, 10, 6, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 4, 3, 0, 4, 7, 3, 6, 5, 10, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 9, 0, 5, 10, 6, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1 },
    { 10, 6, 5, 1, 9, 7, 1, 7, 3, 7, 9, 4, -1, -1, -1, -1 },
    { 6, 1, 2, 6, 5, 1, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 2, 5, 5, 2, 6, 3, 0, 4, 3, 4, 7, -1, -1, -1, -1 },
    { 8, 4, 7, 9, 0, 5, 0, 6, 5, 0, 2, 6, -1, -1, -1, -1 },
    { 7, 3, 9, 7, 9, 4, 3, 2, 9, 5, 9, 6, 2, 6, 9, -1 },
    { 3, 11, 2, 7, 8, 4, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1 },
    { 5, 10, 6, 4, 7, 2, 4, 2, 0, 2, 7, 11, -1, -1, -1, -1 },
    { 0, 1, 9, 4, 7, 8, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1 },
    { 9, 2, 1, 9, 11, 2, 9, 4, 11, 7, 11, 4, 5, 10, 6, -1 },
    { 8, 4, 7, 3, 11, 5, 3, 5, 1, 5, 11, 6, -1, -1, -1, -1 },
    { 5, 1, 11, 5, 11, 6, 1, 0, 11, 7, 11, 4, 0, 4, 11, -1 },
    { 0, 5, 9, 0, 6, 5, 0, 3, 6, 11, 6, 3, 8, 4, 7, -1 },
    { 6, 5, 9, 6, 9, 11, 4, 7, 9, 7, 11, 9, -1, -1, -1, -1 },
    { 10, 4, 9, 6, 4, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 4, 10, 6, 4, 9, 10, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1 },
    { 10, 0, 1, 10, 6, 0, 6, 4, 0, -1, -1, -1, -1, -1, -1, -1 },
    { 8, 3, 1, 8, 1, 6, 8, 6, 4, 6, 1, 10, -1, -1, -1, -1 },
    { 1, 4, 9, 1, 2, 4, 2, 6, 4, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 0, 8, 1, 2, 9, 2, 4, 9, 2, 6, 4, -1, -1, -1, -1 },
    { 0, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 8, 3, 2, 8, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1 },
    { 10, 4, 9, 10, 6, 4, 11, 2, 3, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 8, 2, 2, 8, 11, 4, 9, 10, 4, 10, 6, -1, -1, -1, -1 },
    { 3, 11, 2, 0, 1, 6, 0, 6, 4, 6, 1, 10, -1, -1, -1, -1 },
    { 6, 4, 1, 6, 1, 10, 4, 8, 1, 2, 1, 11, 8, 11, 1, -1 },
    { 9, 6, 4, 9, 3, 6, 9, 1, 3, 11, 6, 3, -1, -1, -1, -1 },
    { 8, 11, 1, 8, 1, 0, 11, 6, 1, 9, 1, 4, 6, 4, 1, -1 },
    { 3, 11, 6, 3, 6, 0, 0, 6, 4, -1, -1, -1, -1, -1, -1, -1 },
    { 6, 4, 8, 11, 6, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 7, 10, 6, 7, 8, 10, 8, 9, 10, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 7, 3, 0, 10, 7, 0, 9, 10, 6, 7, 10, -1, -1, -1, -1 },
    { 10, 6, 7, 1, 10, 7, 1, 7, 8, 1, 8, 0, -1, -1, -1, -1 },
    { 10, 6, 7, 10, 7, 1, 1, 7, 3, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 2, 6, 1, 6, 8, 1, 8, 9, 8, 6, 7, -1, -1, -1, -1 },
    { 2, 6, 9, 2, 9, 1, 6, 7, 9, 0, 9, 3, 7, 3, 9, -1 },
    { 7, 8, 0, 7, 0, 6, 6, 0, 2, -1, -1, -1, -1, -1, -1, -1 },
    { 7, 3, 2, 6, 7, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 2, 3, 11, 10, 6, 8, 10, 8, 9, 8, 6, 7, -1, -1, -1, -1 },
    { 2, 0, 7, 2, 7, 11, 0, 9, 7, 6, 7, 10, 9, 10, 7, -1 },
    { 1, 8, 0, 1, 7, 8, 1, 10, 7, 6, 7, 10, 2, 3, 11, -1 },
    { 11, 2, 1, 11, 1, 7, 10, 6, 1, 6, 7, 1, -1, -1, -1, -1 },
    { 8, 9, 6, 8, 6, 7, 9, 1, 6, 11, 6, 3, 1, 3, 6, -1 },
    { 0, 9, 1, 11, 6, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 7, 8, 0, 7, 0, 6, 3, 11, 0, 11, 6, 0, -1, -1, -1, -1 },
    { 7, 11, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 0, 8, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 1, 9, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 8, 1, 9, 8, 3, 1, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1 },
    { 10, 1, 2, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 2, 10, 3, 0, 8, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1 },
    { 2, 9, 0, 2, 10, 9, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1 },
    { 6, 11, 7, 2, 10, 3, 10, 8, 3, 10, 9, 8, -1, -1, -1, -1 },
    { 7, 2, 3, 6, 2, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 7, 0, 8, 7, 6, 0, 6, 2, 0, -1, -1, -1, -1, -1, -1, -1 },
    { 2, 7, 6, 2, 3, 7, 0, 1, 9, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 6, 2, 1, 8, 6, 1, 9, 8, 8, 7, 6, -1, -1, -1, -1 },
    { 10, 7, 6, 10, 1, 7, 1, 3, 7, -1, -1, -1, -1, -1, -1, -1 },
    { 10, 7, 6, 1, 7, 10, 1, 8, 7, 1, 0, 8, -1, -1, -1, -1 },
    { 0, 3, 7, 0, 7, 10, 0, 10, 9, 6, 10, 7, -1, -1, -1, -1 },
    { 7, 6, 10, 7, 10, 8, 8, 10, 9, -1, -1, -1, -1, -1, -1, -1 },
    { 6, 8, 4, 11, 8, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 6, 11, 3, 0, 6, 0, 4, 6, -1, -1, -1, -1, -1, -1, -1 },
    { 8, 6, 11, 8, 4, 6, 9, 0, 1, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 4, 6, 9, 6, 3, 9, 3, 1, 11, 3, 6, -1, -1, -1, -1 },
    { 6, 8, 4, 6, 11, 8, 2, 10, 1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 2, 10, 3, 0, 11, 0, 6, 11, 0, 4, 6, -1, -1, -1, -1 },
    { 4, 11, 8, 4, 6, 11, 0, 2, 9, 2, 10, 9, -1, -1, -1, -1 },
    { 10, 9, 3, 10, 3, 2, 9, 4, 3, 11, 3, 6, 4, 6, 3, -1 },
    { 8, 2, 3, 8, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 9, 0, 2, 3, 4, 2, 4, 6, 4, 3, 8, -1, -1, -1, -1 },
    { 1, 9, 4, 1, 4, 2, 2, 4, 6, -1, -1, -1, -1, -1, -1, -1 },
    { 8, 1, 3, 8, 6, 1, 8, 4, 6, 6, 10, 1, -1, -1, -1, -1 },
    { 10, 1, 0, 10, 0, 6, 6, 0, 4, -1, -1, -1, -1, -1, -1, -1 },
    { 4, 6, 3, 4, 3, 8, 6, 10, 3, 0, 3, 9, 10, 9, 3, -1 },
    { 10, 9, 4, 6, 10, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 4, 9, 5, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 8, 3, 4, 9, 5, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1 },
    { 5, 0, 1, 5, 4, 0, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1 },
    { 11, 7, 6, 8, 3, 4, 3, 5, 4, 3, 1, 5, -1, -1, -1, -1 },
    { 9, 5, 4, 10, 1, 2, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1 },
    { 6, 11, 7, 1, 2, 10, 0, 8, 3, 4, 9, 5, -1, -1, -1, -1 },
    { 7, 6, 11, 5, 4, 10, 4, 2, 10, 4, 0, 2, -1, -1, -1, -1 },
    { 3, 4, 8, 3, 5, 4, 3, 2, 5, 10, 5, 2, 11, 7, 6, -1 },
    { 7, 2, 3, 7, 6, 2, 5, 4, 9, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 5, 4, 0, 8, 6, 0, 6, 2, 6, 8, 7, -1, -1, -1, -1 },
    { 3, 6, 2, 3, 7, 6, 1, 5, 0, 5, 4, 0, -1, -1, -1, -1 },
    { 6, 2, 8, 6, 8, 7, 2, 1, 8, 4, 8, 5, 1, 5, 8, -1 },
    { 9, 5, 4, 10, 1, 6, 1, 7, 6, 1, 3, 7, -1, -1, -1, -1 },
    { 1, 6, 10, 1, 7, 6, 1, 0, 7, 8, 7, 0, 9, 5, 4, -1 },
    { 4, 0, 10, 4, 10, 5, 0, 3, 10, 6, 10, 7, 3, 7, 10, -1 },
    { 7, 6, 10, 7, 10, 8, 5, 4, 10, 4, 8, 10, -1, -1, -1, -1 },
    { 6, 9, 5, 6, 11, 9, 11, 8, 9, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 6, 11, 0, 6, 3, 0, 5, 6, 0, 9, 5, -1, -1, -1, -1 },
    { 0, 11, 8, 0, 5, 11, 0, 1, 5, 5, 6, 11, -1, -1, -1, -1 },
    { 6, 11, 3, 6, 3, 5, 5, 3, 1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 2, 10, 9, 5, 11, 9, 11, 8, 11, 5, 6, -1, -1, -1, -1 },
    { 0, 11, 3, 0, 6, 11, 0, 9, 6, 5, 6, 9, 1, 2, 10, -1 },
    { 11, 8, 5, 11, 5, 6, 8, 0, 5, 10, 5, 2, 0, 2, 5, -1 },
    { 6, 11, 3, 6, 3, 5, 2, 10, 3, 10, 5, 3, -1, -1, -1, -1 },
    { 5, 8, 9, 5, 2, 8, 5, 6, 2, 3, 8, 2, -1, -1, -1, -1 },
    { 9, 5, 6, 9, 6, 0, 0, 6, 2, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 5, 8, 1, 8, 0, 5, 6, 8, 3, 8, 2, 6, 2, 8, -1 },
    { 1, 5, 6, 2, 1, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 3, 6, 1, 6, 10, 3, 8, 6, 5, 6, 9, 8, 9, 6, -1 },
    { 10, 1, 0, 10, 0, 6, 9, 5, 0, 5, 6, 0, -1, -1, -1, -1 },
    { 0, 3, 8, 5, 6, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 10, 5, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 11, 5, 10, 7, 5, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 11, 5, 10, 11, 7, 5, 8, 3, 0, -1, -1, -1, -1, -1, -1, -1 },
    { 5, 11, 7, 5, 10, 11, 1, 9, 0, -1, -1, -1, -1, -1, -1, -1 },
    { 10, 7, 5, 10, 11, 7, 9, 8, 1, 8, 3, 1, -1, -1, -1, -1 },
    { 11, 1, 2, 11, 7, 1, 7, 5, 1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 8, 3, 1, 2, 7, 1, 7, 5, 7, 2, 11, -1, -1, -1, -1 },
    { 9, 7, 5, 9, 2, 7, 9, 0, 2, 2, 11, 7, -1, -1, -1, -1 },
    { 7, 5, 2, 7, 2, 11, 5, 9, 2, 3, 2, 8, 9, 8, 2, -1 },
    { 2, 5, 10, 2, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1 },
    { 8, 2, 0, 8, 5, 2, 8, 7, 5, 10, 2, 5, -1, -1, -1, -1 },
    { 9, 0, 1, 5, 10, 3, 5, 3, 7, 3, 10, 2, -1, -1, -1, -1 },
    { 9, 8, 2, 9, 2, 1, 8, 7, 2, 10, 2, 5, 7, 5, 2, -1 },
    { 1, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 8, 7, 0, 7, 1, 1, 7, 5, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 0, 3, 9, 3, 5, 5, 3, 7, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 8, 7, 5, 9, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 5, 8, 4, 5, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1 },
    { 5, 0, 4, 5, 11, 0, 5, 10, 11, 11, 3, 0, -1, -1, -1, -1 },
    { 0, 1, 9, 8, 4, 10, 8, 10, 11, 10, 4, 5, -1, -1, -1, -1 },
    { 10, 11, 4, 10, 4, 5, 11, 3, 4, 9, 4, 1, 3, 1, 4, -1 },
    { 2, 5, 1, 2, 8, 5, 2, 11, 8, 4, 5, 8, -1, -1, -1, -1 },
    { 0, 4, 11, 0, 11, 3, 4, 5, 11, 2, 11, 1, 5, 1, 11, -1 },
    { 0, 2, 5, 0, 5, 9, 2, 11, 5, 4, 5, 8, 11, 8, 5, -1 },
    { 9, 4, 5, 2, 11, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 2, 5, 10, 3, 5, 2, 3, 4, 5, 3, 8, 4, -1, -1, -1, -1 },
    { 5, 10, 2, 5, 2, 4, 4, 2, 0, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 10, 2, 3, 5, 10, 3, 8, 5, 4, 5, 8, 0, 1, 9, -1 },
    { 5, 10, 2, 5, 2, 4, 1, 9, 2, 9, 4, 2, -1, -1, -1, -1 },
    { 8, 4, 5, 8, 5, 3, 3, 5, 1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 4, 5, 1, 0, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 8, 4, 5, 8, 5, 3, 9, 0, 5, 0, 3, 5, -1, -1, -1, -1 },
    { 9, 4, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 4, 11, 7, 4, 9, 11, 9, 10, 11, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 8, 3, 4, 9, 7, 9, 11, 7, 9, 10, 11, -1, -1, -1, -1 },
    { 1, 10, 11, 1, 11, 4, 1, 4, 0, 7, 4, 11, -1, -1, -1, -1 },
    { 3, 1, 4, 3, 4, 8, 1, 10, 4, 7, 4, 11, 10, 11, 4, -1 },
    { 4, 11, 7, 9, 11, 4, 9, 2, 11, 9, 1, 2, -1, -1, -1, -1 },
    { 9, 7, 4, 9, 11, 7, 9, 1, 11, 2, 11, 1, 0, 8, 3, -1 },
    { 11, 7, 4, 11, 4, 2, 2, 4, 0, -1, -1, -1, -1, -1, -1, -1 },
    { 11, 7, 4, 11, 4, 2, 8, 3, 4, 3, 2, 4, -1, -1, -1, -1 },
    { 2, 9, 10, 2, 7, 9, 2, 3, 7, 7, 4, 9, -1, -1, -1, -1 },
    { 9, 10, 7, 9, 7, 4, 10, 2, 7, 8, 7, 0, 2, 0, 7, -1 },
    { 3, 7, 10, 3, 10, 2, 7, 4, 10, 1, 10, 0, 4, 0, 10, -1 },
    { 1, 10, 2, 8, 7, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 4, 9, 1, 4, 1, 7, 7, 1, 3, -1, -1, -1, -1, -1, -1, -1 },
    { 4, 9, 1, 4, 1, 7, 0, 8, 1, 8, 7, 1, -1, -1, -1, -1 },
    { 4, 0, 3, 7, 4, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 4, 8, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 0, 9, 3, 9, 11, 11, 9, 10, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 1, 10, 0, 10, 8, 8, 10, 11, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 1, 10, 11, 3, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 2, 11, 1, 11, 9, 9, 11, 8, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 0, 9, 3, 9, 11, 1, 2, 9, 2, 11, 9, -1, -1, -1, -1 },
    { 0, 2, 11, 8, 0, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 2, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 2, 3, 8, 2, 8, 10, 10, 8, 9, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 10, 2, 0, 9, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 2, 3, 8, 2, 8, 10, 0, 1, 8, 1, 10, 8, -1, -1, -1, -1 },
    { 1, 10, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 3, 8, 9, 1, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 9, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 3, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 }
    };

static int[] cornerIndexAFromEdge = new int[12]{
    0,
    1,
    2,
    3,
    4,
    5,
    6,
    7,
    0,
    1,
    2,
    3
};

static int[] cornerIndexBFromEdge = new int[12]{
    1,
    2,
    3,
    0,
    5,
    6,
    7,
    4,
    4,
    5,
    6,
    7
};
}

public class worldNoise
{

    const int X_NOISE_GEN = 1619;
    const int Y_NOISE_GEN = 31337;
    const int Z_NOISE_GEN = 6971;
    const int SEED_NOISE_GEN = 1013;
    const int SHIFT_NOISE_GEN = 8;

    public static int IntValueNoise3D(int x, int y, int z, int seed)
    {
        int n = (
            X_NOISE_GEN * x
            + Y_NOISE_GEN * y
            + Z_NOISE_GEN * z
            + SEED_NOISE_GEN * seed)
        & 0x7fffffff;
        n = (n >> 13) ^ n;
        return (n * (n * n * 60493 + 19990303) + 1376312589) & 0x7fffffff;
    }
    public static double ValueCoherentNoise3D(double x, double y, double z, int seed)
    {
        int x0 = (x > 0.0 ? (int)x : (int)x - 1);
        int x1 = x0 + 1;
        int y0 = (y > 0.0 ? (int)y : (int)y - 1);
        int y1 = y0 + 1;
        int z0 = (z > 0.0 ? (int)z : (int)z - 1);
        int z1 = z0 + 1;
        double xs = 0;
        double ys = 0;
        double zs = 0;

        xs = SCurve5(x - (double)x0);
        ys = SCurve5(y - (double)y0);
        zs = SCurve5(z - (double)z0);
        double n0 = 0;
        double n1 = 0;
        double ix0 = 0;
        double ix1 = 0;
        double iy0 = 0;
        double iy1 = 0;
        n0 = ValueNoise3D(x0, y0, z0, seed);
        n1 = ValueNoise3D(x1, y0, z0, seed);
        ix0 = LinearInterp(n0, n1, xs);
        n0 = ValueNoise3D(x0, y1, z0, seed);
        n1 = ValueNoise3D(x1, y1, z0, seed);
        ix1 = LinearInterp(n0, n1, xs);
        iy0 = LinearInterp(ix0, ix1, ys);
        n0 = ValueNoise3D(x0, y0, z1, seed);
        n1 = ValueNoise3D(x1, y0, z1, seed);
        ix0 = LinearInterp(n0, n1, xs);
        n0 = ValueNoise3D(x0, y1, z1, seed);
        n1 = ValueNoise3D(x1, y1, z1, seed);
        ix1 = LinearInterp(n0, n1, xs);
        iy1 = LinearInterp(ix0, ix1, ys);
        return LinearInterp(iy0, iy1, zs);
    }

    public static double interpQuad(double x, double y, double v1, double v2, double v3, double v4)
    {
        double xs = 0;
        double ys = 0;

        xs = x;// SCurve5 (x);
        ys = y;// SCurve5 (y);
        double n0 = 0;
        double n1 = 0;
        double ix0 = 0;
        double ix1 = 0;
        n0 = v1;
        n1 = v2;
        ix0 = LinearInterp(n0, n1, xs);
        n0 = v3;
        n1 = v4;
        ix1 = LinearInterp(n0, n1, xs);
        return LinearInterp(ix0, ix1, ys);
    }

    public static double ValueNoise3D(int x, int y, int z, int seed)
    {
        return 1.0 - ((double)IntValueNoise3D(x, y, z, seed) / 1073741824.0);
    }

    public static double LinearInterp(double n0, double n1, double a)
    {
        return ((1.0 - a) * n0) + (a * n1);
    }

    public static double SCurve5(double a)
    {
        double a3 = a * a * a;
        double a4 = a3 * a;
        double a5 = a4 * a;
        return (6.0 * a5) - (15.0 * a4) + (10.0 * a3);
    }

}