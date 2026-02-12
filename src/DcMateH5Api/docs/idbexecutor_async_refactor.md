# IDbExecutor 全面非同步化重構說明

## 目標

本次重構聚焦於 `IDbExecutor` 介面收斂與交易一致性，原則如下：

1. 僅保留實際有呼叫的方法。
2. 方法全部為 `Task`/`Task<T>` 非同步簽章。
3. 交易流程統一透過 `TxAsync` 進入。
4. 交易內資料存取一律使用同一組 `conn/tx`。

## 最終介面設計

`IDbExecutor` 保留以下 API（全部 async）：

- `QueryAsync<T>`
- `QueryFirstOrDefaultAsync<T>`
- `ExecuteAsync`
- `ExecuteScalarAsync<T>`
- `TxAsync(...)`
- `TxAsync<T>(...)`
- `ExecuteInTxAsync(...)`
- `ExecuteScalarInTxAsync<T>(...)`
- `QueryInTxAsync<T>(...)`
- `QueryFirstOrDefaultInTxAsync<T>(...)`

已移除未被解決方案使用的：

- `QuerySingleOrDefaultAsync<T>`
- `QuerySingleOrDefaultInTxAsync<T>`

## 交易一致性做法

- `TxAsync` 在內部建立並管理交易生命週期（Begin/Commit/Rollback）。
- 交易建立後，透過 `DbTransactionContext` 將目前交易綁定到 scope。
- 非交易 API 在交易 scope 內執行時，自動吃 ambient transaction。
- 交易內明確傳入 `conn/tx` 的 API（InTx 系列）可保證同一條連線與同一個 transaction。

## 避免 N+1 的建議

若查詢流程需要父子資料，優先策略：

1. 單次 SQL 以 `JOIN` 或 `IN` 取回所需資料。
2. 在記憶體以字典分組映射，避免 `foreach` 逐筆查 DB。
3. 僅在商業邏輯必要時才拆多次查詢。

## Timeout 與 CancellationToken

- 保留原先 `timeoutSeconds` 設定邏輯。
- 以 `CommandDefinition` 統一傳遞 `CancellationToken`。
- 交易 rollback 使用 `CancellationToken.None` 保留既有容錯策略。
