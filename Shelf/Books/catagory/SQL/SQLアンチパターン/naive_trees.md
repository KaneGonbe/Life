### Naive Trees（ナイーブツリー）

## 概要

コメントとリプライのような**再帰的な親子構造**をSQLで扱う際に生じるアンチパターン。

最も素朴な実装（隣接リスト）は設計としては自然だが、取得・削除が難しくなる。

---

## アンチパターン: 隣接リスト（Adjacency List）

```sql
CREATE TABLE Comments (
  comment_id  INT PRIMARY KEY,
  parent_id   INT REFERENCES Comments(comment_id),
  content     TEXT
);
```

```
comment_id | parent_id | content
---------- | --------- | -------
1          | NULL      | 最初のコメント
2          | 1         | ↳ リプライA
3          | 1         | ↳ リプライB
4          | 2         | ↳ ↳ リプライAへのリプライ
5          | 4         | ↳ ↳ ↳ さらに深い
```

### 問題点

**ツリーが何階層あるか事前にわからない**ため、SQLが階層数に依存する。

2階層：
```sql
SELECT c1.*, c2.*
FROM Comments c1
LEFT JOIN Comments c2 ON c2.parent_id = c1.comment_id
WHERE c1.comment_id = 1;
```

3階層になると JOIN を1つ増やす必要がある。階層が増えるたびに **SQL文そのものが変わる**。

---

## 解決策

### 1. 経路列挙（Path Enumeration）

各ノードに「自分までの経路」を文字列で持たせる。

```sql
CREATE TABLE Comments (
  comment_id  INT PRIMARY KEY,
  path        VARCHAR(1000),  -- 例: "1/2/4/"
  content     TEXT
);
```

```sql
-- comment_id=1 配下を全取得
SELECT * FROM Comments WHERE path LIKE '1/%';
```

**メリット**: クエリが簡単
**デメリット**: 文字列制限・外部キー制約なし（Jaywalkingと同様の問題）

---

### 2. 入れ子集合（Nested Sets）

各ノードに `lft` / `rgt` の数値を持たせ、範囲で包含関係を表す。

```sql
CREATE TABLE Comments (
  comment_id  INT PRIMARY KEY,
  lft         INT,
  rgt         INT,
  content     TEXT
);
```

```sql
-- comment_id=1 配下を全取得
SELECT c2.*
FROM Comments c1
JOIN Comments c2 ON c2.lft BETWEEN c1.lft AND c1.rgt
WHERE c1.comment_id = 1;
```

**メリット**: 読み取りが高速
**デメリット**: ノードの追加・削除のたびに大量の行の lft/rgt を更新する必要がある

---

### 3. 閉包テーブル（Closure Table）★推奨

すべての「祖先→子孫」の組み合わせを別テーブルに記録する。

```sql
CREATE TABLE Comments (
  comment_id INT PRIMARY KEY,
  content    TEXT
);

CREATE TABLE TreePaths (
  ancestor    INT REFERENCES Comments(comment_id),
  descendant  INT REFERENCES Comments(comment_id),
  PRIMARY KEY (ancestor, descendant)
);
```

`1 → 2 → 4` の構造の場合、TreePaths の中身：

```
ancestor | descendant
-------- | ----------
1        | 1
1        | 2
1        | 4
2        | 2
2        | 4
4        | 4
```

```sql
-- comment_id=1 配下を全取得
SELECT c.*
FROM Comments c
JOIN TreePaths t ON t.descendant = c.comment_id
WHERE t.ancestor = 1;
```

**メリット**: 読み書きがシンプル・外部キー制約を貼れる
**デメリット**: データ量が増える（ノード数の2乗オーダー）

---

### 4. 再帰CTE（WITH RECURSIVE）

テーブル設計を変えず、SQLの再帰クエリで対応する。

```sql
WITH RECURSIVE subtree AS (
  SELECT * FROM Comments WHERE comment_id = 1
  UNION ALL
  SELECT c.* FROM Comments c
  JOIN subtree s ON c.parent_id = s.comment_id
)
SELECT * FROM subtree;
```

**メリット**: 隣接リストのまま使える・設計変更不要
**デメリット**: DBによってはサポートなし・深い階層でパフォーマンスが落ちることがある

---

## まとめ

| 手法 | 読み取り | 書き込み | 整合性 |
|------|--------|--------|------|
| 隣接リスト | 難（階層依存） | 簡単 | 外部キーあり |
| 経路列挙 | 簡単 | 普通 | 外部キーなし |
| 入れ子集合 | 高速 | 重い | 外部キーなし |
| 閉包テーブル | 簡単 | 簡単 | 外部キーあり |
| 再帰CTE | 簡単 | 簡単 | 外部キーあり |

**推奨: 閉包テーブル**（データ量は増えるが、読み書き両方シンプルで整合性も担保できる）
