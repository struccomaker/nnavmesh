using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class PerformanceTracker : MonoBehaviour
{
    private Queue<PathfindingController.PathfindingMetrics> unityMetrics = new Queue<PathfindingController.PathfindingMetrics>();
    private Queue<PathfindingController.PathfindingMetrics> boundedMetrics = new Queue<PathfindingController.PathfindingMetrics>();
    private int maxSamples = 100;

    public void RecordMetrics(PathfindingController.PathfindingMetrics metrics)
    {
        if (metrics.mode == PathfindingMode.UnityNavMesh)
        {
            unityMetrics.Enqueue(metrics);
            if (unityMetrics.Count > maxSamples)
                unityMetrics.Dequeue();
        }
        else
        {
            boundedMetrics.Enqueue(metrics);
            if (boundedMetrics.Count > maxSamples)
                boundedMetrics.Dequeue();
        }
    }

    public (PathfindingController.PathfindingMetrics unity, PathfindingController.PathfindingMetrics bounded) GetAverageMetrics()
    {
        var unityAvg = CalculateAverage(unityMetrics, PathfindingMode.UnityNavMesh);
        var boundedAvg = CalculateAverage(boundedMetrics, PathfindingMode.BoundedAStar);
        return (unityAvg, boundedAvg);
    }

    private PathfindingController.PathfindingMetrics CalculateAverage(Queue<PathfindingController.PathfindingMetrics> metrics, PathfindingMode mode)
    {
        if (metrics.Count == 0)
            return new PathfindingController.PathfindingMetrics(mode);

        var result = new PathfindingController.PathfindingMetrics(mode);
        var list = metrics.ToList();

        result.searchTimeMs = list.Average(m => m.searchTimeMs);
        result.trianglesEvaluated = (int)list.Average(m => m.trianglesEvaluated);
        result.trianglesInBounds = (int)list.Average(m => m.trianglesInBounds);
        result.pathLength = list.Average(m => m.pathLength);
        result.tacticalScore = list.Average(m => m.tacticalScore);

        return result;
    }

    public void ClearMetrics()
    {
        unityMetrics.Clear();
        boundedMetrics.Clear();
        Debug.Log("Performance metrics cleared");
    }

    public int GetSampleCount(PathfindingMode mode)
    {
        return mode == PathfindingMode.UnityNavMesh ? unityMetrics.Count : boundedMetrics.Count;
    }
}