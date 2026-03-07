### Jaywalk

## 概要

1つのカラムにカンマ区切りで複数の値を詰め込むアンチパターン。
1対多だった関係が多対多に変わったときに特に問題が顕在化する。

```sql
-- アンチパターンの例
CREATE TABLE Products (
  product_id  INT PRIMARY KEY,
  account_ids VARCHAR(100)  -- "1,2,3" のように詰め込む
);
```

---

## 問題点

### 1. 検索が複雑・不正確になる

`LIKE '%2%'` では「12」「21」「123」なども引っかかってしまう。
正確に検索しようとすると条件が複雑になる。

```sql
-- account_id=2 を正確に検索しようとすると…
WHERE account_ids = '2'
   OR account_ids LIKE '2,%'
   OR account_ids LIKE '%,2,%'
   OR account_ids LIKE '%,2'
```

### 2. 削除・更新が困難

特定のIDを削除・変更したくても、文字列の中を操作しなければならない。
上記の検索と同様に複雑なWHERE条件が必要になる。

```sql
-- "1,2,3" から 2 だけ消したい → 文字列操作が必要で非常に複雑
```

### 3. 集計が困難

「各アカウントに紐づく製品数を数えたい」といった集計も、
カンマ区切りの文字列を解析しながら行う必要があり困難。

```sql
-- 本来なら COUNT で済むはずが、文字列の中身を見ながら集計しなければならない
```

### 4. 外部キー制約が貼れず整合性を保証できない

DBは `VARCHAR` の文字列の中身を解釈できないため、`REFERENCES` が使えない。
Accounts側でIDを削除しても、Products側の文字列にIDが残り続ける（宙ぶらりん参照）。

```sql
-- 中間テーブルがあれば整合性をDBに任せられる
CREATE TABLE ProductAccounts (
  product_id INT REFERENCES Products(product_id),
  account_id INT REFERENCES Accounts(account_id)
);

-- Jaywalkingでは↑のようなREFERENCESが貼れない
-- アプリ側でケアしないとデータ不整合が起きる
```

### 5. カラムの文字数制限によって登録数が制約される

`VARCHAR(100)` のように文字数で上限を決めると、IDの桁数や件数によって
実際に登録できる数が変わる。上限を増やすにはスキーマ変更が必要。

```sql
-- IDが増えると "1,2,3,4,5,...,99,100" と文字数がどんどん伸びる
-- VARCHAR(100) で何件入るかはIDの桁数次第で予測不能
account_ids VARCHAR(100)  -- いつか溢れる
```

---

## 解決策（基本）

中間テーブル（交差テーブル）を使って多対多を正規化する。

```sql
CREATE TABLE ProductAccounts (
  product_id INT REFERENCES Products(product_id),
  account_id INT REFERENCES Accounts(account_id),
  PRIMARY KEY (product_id, account_id)
);
```
