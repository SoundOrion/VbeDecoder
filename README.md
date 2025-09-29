# VbeDecoder

## 使い方

```bash
dotnet new console -n VbeDecode
cd VbeDecode
# Program.cs を下のコードで丸ごと置き換え
dotnet run -- -i sample.vbe -o decoded.vbs --encoding cp932
# 生バイト出力（文字化けさせたくない時）
dotnet run -- -i sample.vbe --raw -o decoded.bin
```

* `-i` / `--input`: 入力 `.vbe`（必須）
* `-o` / `--output`: 出力ファイル（省略時は標準出力）
* `--encoding`: 出力文字コード（例: `utf-8`, `utf-16`, `cp932`）。省略時は **BOM自動判定 → cp932**。
* `--raw`: 復号後の**生バイト**をそのまま書き出し（文字コード変換しない）

