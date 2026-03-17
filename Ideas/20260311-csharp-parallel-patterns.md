# C# 並列処理パターンの使い分け

参照コード: `OnProcess/C/ProcessPararel/ParallelTaskRunner.cs`

---

## 使い分けの観点

### 1. リソース制約があるか？

| 状況 | 使うパターン |
|------|-------------|
| 制約なし（件数が少ない、外部APIなし） | `Task.WhenAll` そのまま |
| 外部APIのレート制限・DBコネクション上限あり | `SemaphoreSlim` で同時実行数を制限 |

**判断基準**: 「全部同時に投げても壊れないか？」を問う。叩き先がボトルネックになるならセマフォで絞る。

---

### 2. 途中で止める必要があるか？

| 状況 | 使うパターン |
|------|-------------|
| 最後まで全部完走させる | キャンセルなし |
| タイムアウト・ユーザー中断・条件達成で打ち切る | `CancellationToken` |

**判断基準**: 「処理が長引いたとき、何もできないのは困るか？」を問う。

---

### 3. 結果をどう扱うか？

| 要件 | 手段 |
|------|------|
| 全部成功したら使う | `Task.WhenAll`（1件でも失敗すると例外） |
| 失敗してもほかの結果は使いたい | `Task.WhenAll` + 個別の `task.Status` チェック |
| 最初に終わった1件だけ使う | `Task.WhenAny` |

---

### 4. CPU処理 vs I/O処理か？

| 処理の性質 | 向くアプローチ |
|------------|--------------|
| I/O待ち（HTTP・DB・ファイル） | `async/await` + `Task.WhenAll` |
| CPU負荷の高い計算 | `Parallel.ForEach` / `PLINQ` |

> `Task.WhenAll` に CPU 処理を渡してもスレッドプールを食うだけで効率が悪い。

---

## 選択フローチャート

```
並列処理が必要
  ├─ CPU処理？ → Parallel.ForEach / PLINQ
  └─ I/O処理？
        ├─ リソース制限あり？ → SemaphoreSlim + Task.WhenAll
        ├─ 中断が必要？      → CancellationToken 付き Task.WhenAll
        └─ それ以外          → Task.WhenAll そのまま
```

---

## パターン別コード概要

### Pattern 1: 基本的な並列実行（`Task.WhenAll`）

```csharp
var tasks = new[]
{
    SimulateWork("タスクA", durationMs: 1000),
    SimulateWork("タスクB", durationMs: 1500),
};
string[] results = await Task.WhenAll(tasks);
```

### Pattern 2: 同時実行数を制限（`SemaphoreSlim`）

```csharp
var semaphore = new SemaphoreSlim(maxConcurrency: 2);

var tasks = workItems.Select(async id =>
{
    await semaphore.WaitAsync();
    try   { return await SimulateWork($"ジョブ{id}", durationMs: 600); }
    finally { semaphore.Release(); }
});

string[] results = await Task.WhenAll(tasks);
```

### Pattern 3: キャンセル対応（`CancellationToken`）

```csharp
using var cts = new CancellationTokenSource();
cts.CancelAfter(TimeSpan.FromMilliseconds(1200)); // 1.2秒でタイムアウト

var tasks = new[]
{
    SimulateWork("短いタスク", durationMs: 500,  ct: cts.Token),
    SimulateWork("長いタスク", durationMs: 2000, ct: cts.Token),
};

try
{
    string[] results = await Task.WhenAll(tasks);
}
catch (OperationCanceledException)
{
    // タイムアウト or キャンセル時の処理
}
```
