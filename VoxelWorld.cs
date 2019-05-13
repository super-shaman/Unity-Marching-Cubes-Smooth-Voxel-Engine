using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;
using System.Threading;

public class VoxelWorld : MonoBehaviour
{

    public VoxelChunk Chunk;
    public Player player;
    public static int loadSize = 16;
    public static int loadHeight = 16;
    VoxelChunk[,,] chunks = new VoxelChunk[loadSize, loadSize, loadHeight];

    void Start()
    {
        Stopwatch stopWatch = new Stopwatch();
        stopWatch.Reset();
        stopWatch.Start();
        for (int i = 0; i < loadSize; i++)
        {
            for (int ii = 0; ii < loadSize; ii++)
            {
                for (int iii = 0; iii < loadHeight; iii++)
                {
                    VoxelChunk c = Instantiate<VoxelChunk>(Chunk);
                    c.LoadChunk(-loadSize / 2 + i, -loadSize / 2 + ii, -loadHeight / 2 + iii);
                    chunks[i, ii, iii] = c;
                }
            }
        }
        for (int i = 0; i < loadSize; i++)
        {
            for (int ii = 0; ii < loadSize; ii++)
            {
                for (int iii = 0; iii < loadHeight; iii++)
                {
                    VoxelChunk[,,] chunkers = new VoxelChunk[3, 3, 3];
                    for (int ier = 0; ier < 3; ier++)
                    {
                        for (int iier = 0; iier < 3; iier++)
                        {
                            for (int iiier = 0; iiier < 3; iiier++)
                            {
                                int o = i - 1 + ier < 0 ? loadSize - 1 : i-1+ier >= loadSize ? 0 : i - 1 + ier;
                                int oo = ii - 1 + iier < 0 ? loadSize - 1 : ii - 1 + iier >= loadSize ? 0 : ii - 1 + iier;
                                int ooo = iii - 1 + iiier < 0 ? loadHeight - 1 : iii - 1 + iiier >= loadHeight ? 0 : iii - 1 + iiier;
                                chunkers[ier, iier, iiier] = chunks[o,oo,ooo];
                            }
                        }
                    }
                    chunks[i, ii, iii].loadChunks(chunkers);
                }
            }
        }
        stopWatch.Stop();
        UnityEngine.Debug.Log(stopWatch.ElapsedMilliseconds);
        /*stopWatch.Reset();
        stopWatch.Start();
        for (int i = 1; i < loadSize-1; i++)
        {
            for (int ii = 1; ii < loadSize-1; ii++)
            {
                for (int iii = 1; iii < loadHeight - 1; iii++)
                {
                    VoxelChunk[,,] chunkers = new VoxelChunk[3, 3, 3];
                    for (int ier = 0; ier < 3; ier++)
                    {
                        for (int iier = 0; iier < 3; iier++)
                        {
                            for (int iiier = 0; iiier < 3; iiier++)
                            {
                                chunkers[ier, iier, iiier] = chunks[i - 1 + ier, ii - 1 + iier, iii - 1 + iiier];
                            }
                        }
                    }
                    chunks[i, ii, iii].loadChunks(chunkers);
                    chunks[i, ii, iii].Load();
                }
            }
        }
        stopWatch.Stop();
        UnityEngine.Debug.Log(stopWatch.ElapsedMilliseconds);
        stopWatch.Reset();
        stopWatch.Start();
        for (int i = 2; i < loadSize - 2; i++)
        {
            for (int ii = 2; ii < loadSize - 2; ii++)
            {
                for (int iii = 2; iii < loadHeight - 2; iii++)
                {
                    chunks[i, ii, iii].Smooth();
                }
            }
        }
        stopWatch.Stop();
        UnityEngine.Debug.Log(stopWatch.ElapsedMilliseconds);*/
        if (VoxelChunk.numThreads < VoxelChunk.maxNumThreads())
        {
            if (thread == null)
            {
                thread = new Thread(VoxelChunk.Run);
                pos = player.pos;
                VoxelChunk.numThreads++;
                VoxelChunk.RunLoadGraphics();
                thread.Start();
            }
            else if (VoxelChunk.Done)
            {
                VoxelChunk.Done = false;
                thread = new Thread(VoxelChunk.Run);
                pos = player.pos;
                VoxelChunk.RunLoadGraphics();
                VoxelChunk.numThreads++;
                thread.Start();
            }
        }
    }

    public static Vector3 pos;
    public static Plane[] planes;
    Thread thread;

    // Update is called once per frame
    void Update()
    {
        planes = player.planes;
        pos = player.pos;
        //UnityEngine.Debug.Log(VoxelChunk.numThreads);
        VoxelChunk.RunLoadGraphics();
    }
}
