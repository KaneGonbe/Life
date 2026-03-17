using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 並列タスク実行のサンプル実装
/// </summary>
class ParallelTaskRunner
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== 並列タスク実行デモ ===\n");

        // 1. 基本的な並列実行
        await RunBasicParallel();

        // 2. 最大同時実行数を制限した並列実行
        await RunWithConcurrencyLimit();

        // 3. キャンセル対応の並列実行
        await RunWithCancellation();
    }

    // -------------------------------------------------------
    // 1. Task.WhenAll による基本的な並列実行
    // -------------------------------------------------------
    static async Task RunBasicParallel()
    {
        Console.WriteLine("--- 1. 基本的な並列実行 ---");

        var sw = Stopwatch.StartNew();

        var tasks = new[]
        {
            SimulateWork("タスクA", durationMs: 1000),
            SimulateWork("タスクB", durationMs: 1500),
            SimulateWork("タスクC", durationMs:  800),
        };

        // 全タスクが完了するまで待機（並列実行）
        string[] results = await Task.WhenAll(tasks);

        sw.Stop();
        Console.WriteLine($"全タスク完了: {string.Join(", ", results)}");
        Console.WriteLine($"所要時間: {sw.ElapsedMilliseconds}ms（直列なら約3300ms）\n");
    }

    // -------------------------------------------------------
    // 2. SemaphoreSlim で最大同時実行数を制限
    // -------------------------------------------------------
    static async Task RunWithConcurrencyLimit()
    {
        Console.WriteLine("--- 2. 同時実行数を制限（最大2件）---");

        const int maxConcurrency = 2;
        var semaphore = new SemaphoreSlim(maxConcurrency);

        var workItems = Enumerable.Range(1, 5).ToList();

        var sw = Stopwatch.StartNew();

        var tasks = workItems.Select(async id =>
        {
            await semaphore.WaitAsync();
            try
            {
                return await SimulateWork($"ジョブ{id}", durationMs: 600);
            }
            finally
            {
                semaphore.Release();
            }
        });

        string[] results = await Task.WhenAll(tasks);

        sw.Stop();
        Console.WriteLine($"完了: {string.Join(", ", results)}");
        Console.WriteLine($"所要時間: {sw.ElapsedMilliseconds}ms\n");
    }

    // -------------------------------------------------------
    // 3. CancellationToken によるキャンセル対応
    // -------------------------------------------------------
    static async Task RunWithCancellation()
    {
        Console.WriteLine("--- 3. キャンセル対応の並列実行 ---");

        using var cts = new CancellationTokenSource();

        // 1.2秒後にキャンセルを発行
        cts.CancelAfter(TimeSpan.FromMilliseconds(1200));

        var tasks = new[]
        {
            SimulateWork("短いタスク", durationMs: 500,  ct: cts.Token),
            SimulateWork("長いタスク", durationMs: 2000, ct: cts.Token),
        };

        try
        {
            string[] results = await Task.WhenAll(tasks);
            Console.WriteLine($"完了: {string.Join(", ", results)}");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("タスクがキャンセルされました（想定通り）");
        }

        // 個別に結果を確認する場合
        foreach (var (task, name) in tasks.Zip(new[] { "短いタスク", "長いタスク" }))
        {
            string status = task.Status switch
            {
                TaskStatus.RanToCompletion => $"成功: {task.Result}",
                TaskStatus.Canceled        => "キャンセル済み",
                TaskStatus.Faulted         => $"エラー: {task.Exception?.InnerException?.Message}",
                _                          => task.Status.ToString()
            };
            Console.WriteLine($"  {name} → {status}");
        }
    }

    // -------------------------------------------------------
    // 疑似的な非同期処理（実際の処理に置き換える）
    // -------------------------------------------------------
    static async Task<string> SimulateWork(
        string name,
        int durationMs,
        CancellationToken ct = default)
    {
        Console.WriteLine($"  [{name}] 開始");
        await Task.Delay(durationMs, ct);
        Console.WriteLine($"  [{name}] 完了");
        return $"{name}:OK";
    }
}
