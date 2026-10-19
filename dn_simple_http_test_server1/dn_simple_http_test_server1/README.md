# dn_simple_http_test_server1 R01

DNNT 261018_WLVW6Y ローカルホスト用軽量 HTTP サーバー + Chrome ランチャー

指定したローカルファイルまたはディレクトリを `127.0.0.1` と `::1` で公開し、Windows 上の Google Chrome にページを開くコンソールアプリケーションです。C# ソースは 1 ファイル、Windows 用の対象は .NET Framework 4.8、言語バージョンは C# 7.3、CPU は AnyCPU です。アプリケーションの実行に追加 NuGet パッケージは必要ありません。

入力仕様、設計判断、仕様の曖昧さ、作業内容および実際の検証結果は [変更履歴](dnnt_261018_wlvw6y_local_http_launcher_history.md) に記載しています。元の指示は [specification.md](specification.md) に収録しています。

## ビルド済み EXE ですぐ使う

ZIP には `bin\Release\` に **.NET Framework 4.8 向けのビルド済み EXE と設定ファイル**を同梱しています。.NET Framework 4.8 または互換性のある後継 4.x ランタイムを備えた Windows では、自分でビルドするための Visual Studio は不要です。

1. ZIP を展開します。
2. 展開先をカレントディレクトリとするコマンドプロンプトから、次のように対象ファイルのフルパスを渡します。
3. 表示されたコンソールを開いたまま使い、終了するときは `q` を押します。

```bat
bin\Release\dnnt_261018_wlvw6y_local_http_launcher.exe "C:\tmp\web\index.html"
```

同じディレクトリの `dnnt_261018_wlvw6y_local_http_launcher.exe.config` も EXE と一緒に保持してください。ディレクトリ全体を指定する例や再表示操作は「3. 起動方法」を参照してください。

この EXE は Linux 上の MSBuild 18.9 と Microsoft 公式の .NET Framework 4.8 参照アセンブリを使用して生成しています。Windows 実機での起動と Chrome の動作は未検証です。HTTP の実通信検証では、この .NET Framework 4.8 向け EXE を Linux の .NET 10.0.13 からそのまま起動する構成を用います。具体的な実施結果は変更履歴と同梱の試験結果に記載します。

## 1. 同梱ファイル

| ファイル | 内容 |
| --- | --- |
| `dnnt_261018_wlvw6y_local_http_launcher_r01_261018_wlvw6y.cs` | アプリケーション全体を収めた唯一の C# ソース |
| `dn_simple_http_test_server1-VS2026.sln` | Visual Studio 2026 用ソリューション |
| `dn_simple_http_test_server1.csproj` | .NET Framework 4.8 用の従来形式プロジェクト。SDK 形式・PackageReference を使用しない |
| `app.config` | .NET Framework 4.8 とパス処理の設定 |
| `bin/Release/dnnt_261018_wlvw6y_local_http_launcher.exe` | .NET Framework 4.8 向けのビルド済みアプリケーション |
| `bin/Release/dnnt_261018_wlvw6y_local_http_launcher.exe.config` | ビルド済み EXE と同じディレクトリで使用する実行時設定 |
| `dnnt_261018_wlvw6y_local_http_launcher_history.md` | 仕様、実装内容、判断理由、検証結果、未検証範囲 |
| `specification.md` | 入力された指示の保存版 |
| `VERIFICATION.md` | ビルド結果、実通信試験の集計、タイムアウト実測値、未検証範囲 |
| `test-results/` | 試験結果の JSON・テキスト、ビルド記録、サーバーログ |
| `SHA256SUMS.txt` | 同梱ファイルの SHA-256 チェックサム |
| `tests/HttpSmoke.net10.csproj` | 同一 C# ソースを Linux 等の .NET 10 で実行する検証用プロジェクト |
| `tests/http_integration_tests.py` | Python 標準ライブラリによる HTTP 実通信試験 |

実行ファイルの内部名・出力名は `dnnt_261018_wlvw6y_local_http_launcher.exe` です。

## 2. Windows で自分でビルドする場合

1. Visual Studio 2026 に「.NET デスクトップ開発」および .NET Framework 4.8 の開発用ターゲティングパックを用意します。実行用ランタイムだけでは、開発用の参照アセンブリを満たさない場合があります。
2. ZIP をディレクトリに展開し、`dn_simple_http_test_server1-VS2026.sln` を開きます。
3. 構成を `Release`、プラットフォームを `Any CPU` にしてビルドします。
4. 出力は `bin\Release\` に生成されます。実行時は EXE と同じディレクトリに生成された `dnnt_261018_wlvw6y_local_http_launcher.exe.config` を置いてください。

Visual Studio の Developer Command Prompt からもビルドできます。

```bat
msbuild dn_simple_http_test_server1-VS2026.sln /t:Build /p:Configuration=Release /p:Platform="Any CPU"
```

主プロジェクトは従来形式の .NET Framework プロジェクトです。追加の .NET 10 SDK や NuGet パッケージの復元は Windows 用ビルドの前提にしていません。`tests/` の .NET 10 プロジェクトは別の検証用です。

### Exe / WinExe の解釈

共通 CODE_RULE の「WinExe」という語と、今回の個別仕様の「コンソール画面」「q / e / o の対話操作」には解釈の余地があります。本版では個別仕様を優先し、`OutputType=Exe` としました。起動するとコンソールを利用できる Windows アプリケーションになります。

## 3. 起動方法

ファイルまたはディレクトリの **完全なパスを 1 個だけ** 指定します。空白を含むパスは引用符で囲みます。

```bat
bin\Release\dnnt_261018_wlvw6y_local_http_launcher.exe "C:\tmp\web\index.html"
```

```bat
bin\Release\dnnt_261018_wlvw6y_local_http_launcher.exe "C:\tmp\web"
```

```bat
bin\Release\dnnt_261018_wlvw6y_local_http_launcher.exe "\\server\share\web\a.html"
```

ファイル指定では、そのファイルがある親ディレクトリが HTTP の `/` となり、指定ファイルの URL を開きます。ディレクトリ指定では、そのディレクトリ自体が `/` となり、`/` を開きます。たとえば `C:\tmp\web\a.html` を指定した場合、同じディレクトリの JavaScript、CSS、画像なども相対 URL で取得できます。

相対パス、`C:relative`、現在のドライブに依存する `\relative` は受け付けません。通常のドライブ絶対パス、完全な UNC、および `\\?\C:\...` / `\\?\UNC\server\share\...` 形式を判定できます。`app.config` は旧式パス処理と長いパスの一律ブロックを無効にしますが、対象ファイルシステムや OS 側の制約までなくすものではありません。

引数なし・引数数不正・絶対パスでない場合は使い方を表示して終了コード `2`、指定対象にアクセスできない場合やサーバーの起動失敗はエラーを表示して終了コード `1` になります。

### 起動後の表示と操作

空いていれば `7101` を使用します。コンソールには、実際に確保したポート番号と URL が表示されます。

```text
dn_simple_http_test_server1: TCP ポート番号 7101 で Listen 中
http://127.0.0.1:7101/a.html
```

ファイル名中の日本語、空白、`+`、`#`、`?`、`%` などは URL の 1 セグメントとして符号化します。

Windows ではレジストリの App Paths、Chrome のコマンド登録、標準配置先から実在する `chrome.exe` を探し、`--new-window` と URL を渡して起動します。通常モードの新規ウインドウを要求します。デバッグモードやブラウザーのセキュリティ無効化スイッチは指定しません。Chrome が見つからない、または起動に失敗した場合でも HTTP サーバーは続きます。

```text
q: 終了、e または o: Web ブラウザでページを再度開く?>
```

- `q`: プロセスを直ちに終了します。クリーンアップの完了は待ちません。
- `e` または `o`: 初回と同じ URL を Chrome に再度開きます。
- 通常のコンソールでは Enter は不要です。大文字の `Q` / `E` / `O` も使用できます。
- 標準入力をパイプ等へリダイレクトした場合、その入力が EOF になっても HTTP サーバーは継続します。操作できなくなったプロンプトの表示は止まり、外部からプロセスを停止して終了します。

HTTP エラーはコンソールに記録され、操作中であればログの後にプロンプトを表示します。

## 4. 公開範囲とポートの確保

Bind / Listen するアドレスは `127.0.0.1` と `::1` だけです。同じ TCP ポート番号を両方で確保できてからサーバーを開始します。IPv4 または IPv6 の片方だけで開始することはありません。

候補は `7101` から `8100` までの 1000 ポートです。全ローカルアドレスの TCP listener と UDP endpoint を OS の API で調べ、使用中の番号を除外したうえで、順に実際の Bind / Listen を試します。スナップショット取得後に他プロセスが取得した競合も、この段階で処理します。

ディレクトリリスティングでは、次の順で既定ページを探します。

1. `index.html`
2. `index.htm`
3. `default.html`
4. `default.htm`

既定ページがなければディレクトリ内のファイルとサブディレクトリを一覧表示します。パスの `..` などによって公開ルートの外へ移動することは拒否します。一方、仕様に従い、公開ディレクトリ内のシンボリックリンクやジャンクションの行き先は制限しません。

### NoLinger と同じポートの再利用

ソケットは Nagle を無効にし、`LingerOption(false, 0)` により Linger を無効にします。これは TCP の状態や OS のポート再利用規則をなくす設定ではありません。

Windows では別プロセスによる二重 Bind を防ぐため、待受けに `ExclusiveAddressUse=true` を使います。直前の接続状態などにより同じポートへの再 Bind が OS に拒否される場合は、次の空き候補へ進みます。従って、本アプリケーションは、Windows 上で終了後も **必ず同じ番号を即時再利用できるという保証はしません**。Linux 側では `ReuseAddress` を使用します。この仕様解釈の詳細と根拠は変更履歴に記載しています。

## 5. HTTP の機能と制限

静的ファイルの `GET` / `HEAD` / `OPTIONS`、HTTP/1.0 と HTTP/1.1 の通常の接続管理、Range リクエスト、ディレクトリ一覧に対応します。圧縮、CGI、ファイルアップロードの保存機能は実装していません。

HTTP/0.9 はバージョン指定のない `GET /path` と本文だけの応答として扱います。HTTP/0.9 にステータス行やヘッダーは存在しないため、次節の固定ヘッダーも HTTP/0.9 には付けません。これは、HTTP/0.9 対応と「常にヘッダーを応答」という要求を両立させるための解釈です。

送受信が無通信のまま 300 秒を超えた接続を切断します。非同期 I/O でも実際の送受信活動を監視します。この切断自体のログ表示は行いません。

### 入力上限

| 項目 | 上限 |
| --- | ---: |
| リクエスト本文 | 64 MiB = 67,108,864 バイト |
| リクエスト行／ヘッダーの 1 行 | 16 KiB = 16,384 バイト |
| ヘッダー全体 | 64 KiB = 65,536 バイト |
| ヘッダーフィールド数 | 100 |
| Range で指定する範囲の数 | 16 |

これらはリクエストの解析に対する上限です。配信ファイル全体を 64 MiB 以下に限定するという意味ではありません。リクエスト本文を受け取る場合は上限内で読み捨て、静的ファイルを上書きしません。具体的な不正形式の扱い、条件付きリクエストや Range の詳細は変更履歴を参照してください。

Range の指定数が 16 個を超える場合は、その Range を無視して全体応答に戻します。範囲の個数制限と、リクエスト行や本文の上限超過に対するエラー処理は区別しています。

### ETag の強さ

ファイル更新時刻とサイズを用いる ETag は、内容全体のハッシュではないため `W/` の付いた弱い ETag として扱います。同じサイズ・同じ更新時刻を維持したまま外部で書き換えられると、値が変わらない可能性があります。強い同一性が必要な判定では、この ETag を強い ETag と見なしません。

`If-Range` が付いた GET では、その値が ETag でも日時でも、本版は Range を無視し、通常のファイル取得では全体を `200 OK` で返します。メタデータから強い validator を生成せず、大きいファイルの内容全体を起動や要求のたびにハッシュ計算する処理も追加しない、という設計です。`If-Range` のない通常の Range リクエストには部分応答 `206 Partial Content` で対応します。前提条件の不成立など別の応答条件がある場合は、その判定が優先されます。

## 6. 固定応答ヘッダー

HTTP/1.0・HTTP/1.1 の応答には、入力仕様の次の値を付加します。成功応答だけでなく HTTP エラー応答にも適用します。

```http
Access-Control-Allow-Origin: *
Access-Control-Allow-Methods: GET, HEAD, OPTIONS
Access-Control-Allow-Headers: *
Access-Control-Max-Age: 86400
Cross-Origin-Resource-Policy: cross-origin
Cross-Origin-Opener-Policy: same-origin
Cross-Origin-Embedder-Policy: require-corp
Cache-Control: no-store
```

値は指定どおり固定です。`Cross-Origin-Embedder-Policy: require-corp` のため、外部サイトのリソースを埋め込む HTML では、相手側の CORS / CORP 応答も影響します。`Access-Control-Allow-Origin: *` は資格情報を伴う任意のクロスオリジン要求を無条件で許可する指定ではありません。

## 7. MIME マッピング

入力仕様に含まれる MIME マッピングの **1890 行、大小文字を区別しない拡張子として 1296 種**をコード中に収録しています。同じ拡張子が複数回登場する場合は後に書かれた値を使います。

複合拡張子は長いものを優先します。たとえば `.exe.config` や `.dll.config` に固有の登録があれば、末尾の `.config` より先に適用します。このため一般的な `.config` の特別上書きは、より具体的な複合拡張子の登録を消しません。

特別上書きの `.sh` / `.md` / `.config` は `text/plain;charset=UTF-8`、追加登録 `.ovpn` は `application/x-openvpn-profile` です。該当する登録がなければ `application/octet-stream` を返します。指定リストの値を尊重し、拡張子ごとに別途「最新の MIME」へ置き換えていません。

## 8. Linux / .NET 10 での再検証

### 同梱の .NET Framework 4.8 向け EXE をそのまま検証する

納品物の HTTP 実通信試験では、`bin/Release/` の .NET Framework 4.8 向け EXE を互換 API のある .NET 10.0.13 から実行します。これにより Windows 向けにコンパイルした成果物自体を試験対象にします。Windows 固有処理はプログラム内の実行時 OS 判定で分岐します。

Linux でこの構成を再現する場合は、.NET 10.0.13 のランタイムを用意し、検証用の作業ディレクトリに次の `net48.runtimeconfig.json` を UTF-8 で作成してください。この互換実行専用ファイルは、Windows 用の `bin/Release/` には同梱していません。

```json
{
  "runtimeOptions": {
    "tfm": "net10.0",
    "framework": {
      "name": "Microsoft.NETCore.App",
      "version": "10.0.13"
    }
  }
}
```

ZIP 展開先をカレントディレクトリとして、検証用設定ファイルを指定して起動します。

```sh
dotnet exec --runtimeconfig /absolute/path/to/scratch/net48.runtimeconfig.json \
  bin/Release/dnnt_261018_wlvw6y_local_http_launcher.exe \
  /absolute/path/to/web/index.html
```

Linux では Chrome の自動起動を省略し、コンソールに URL を表示します。Windows レジストリの参照は実行しません。

### 同じソースを .NET 10 向けに再コンパイルする場合

検証用プロジェクトは、主ソースを `Link` でそのまま参照します。ソースの書換え、Linux 専用 C# の追加、条件付きソースの切り貼りは行いません。Windows 固有処理はアプリケーション内の実行時 OS 判定で分岐します。

.NET 10 SDK がある環境で、展開先をカレントディレクトリとして実行します。

```sh
dotnet build tests/HttpSmoke.net10.csproj -c Release
dotnet tests/bin/Release/net10.0/dnnt_261018_wlvw6y_local_http_launcher.dll /absolute/path/to/web/index.html
```

この方法で生成される .NET 10 向け DLL は、同梱している .NET Framework 4.8 向け EXE とは別のビルドです。

### HTTP 統合試験の再実行

Python 3.9 以降を用意します。HTTP 試験は Python の標準ライブラリだけで動作します。次の `/absolute/path/to/package` は ZIP の展開先、`/absolute/path/to/scratch/net48.runtimeconfig.json` は上で作成した互換実行用設定の絶対パスへ置き換えてください。`--command` にはプログラムの起動コマンドまでを指定し、公開ルートは試験側が一時ディレクトリを用意して末尾に付加します。

```sh
python3 tests/http_integration_tests.py \
  --command 'dotnet exec --runtimeconfig /absolute/path/to/scratch/net48.runtimeconfig.json /absolute/path/to/package/bin/Release/dnnt_261018_wlvw6y_local_http_launcher.exe' \
  --source /absolute/path/to/package/dnnt_261018_wlvw6y_local_http_launcher_r01_261018_wlvw6y.cs \
  --spec /absolute/path/to/package/specification.md \
  --output-dir /absolute/path/to/package/test-results/net48
```

パスに空白を含む場合は、各引数をシェルに合わせて引用し、`--command` 内の EXE や設定ファイルのパスも引用してください。.NET 10 向けに再コンパイルした DLL を試験する場合は、`--command` を `dotnet /absolute/path/to/package/tests/bin/Release/net10.0/dnnt_261018_wlvw6y_local_http_launcher.dll` に置き換えます。

`--idle-test` を追加すると、300 秒の無通信タイムアウトを実際に待って確認するため、約 5 分以上かかります。出力先には JSON / テキスト形式の試験結果とサーバーのログが保存されます。実際に実行した試験の環境・結果・件数は変更履歴および同梱の試験結果を参照してください。

Windows 上の Visual Studio 2026 による実ビルド・実行、Windows レジストリの実機探索、Windows 固有のネットワークテーブル取得、Chrome の実ウインドウ起動については、この Linux 作業環境での実機検証対象に含めていません。特に Chrome の実起動試験は、入力仕様でも不要とされています。

## 9. 参照資料

実装上の具体的な判断と追加資料は変更履歴に記載しています。

- [Microsoft: .NET Framework 開発者向けインストール](https://learn.microsoft.com/en-us/dotnet/framework/install/guide-for-developers)
- [Microsoft: AppContextSwitchOverrides](https://learn.microsoft.com/en-us/dotnet/framework/configure-apps/file-schema/runtime/appcontextswitchoverrides-element)
- [Microsoft: TCP listener の列挙](https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.ipglobalproperties.getactivetcplisteners?view=netframework-4.8.1)
- [Microsoft: UDP endpoint の列挙](https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.ipglobalproperties.getactiveudplisteners?view=netframework-4.8.1)
- [Microsoft: App Paths によるアプリケーション登録](https://learn.microsoft.com/en-us/windows/win32/shell/app-registration)
- [Microsoft: SO_REUSEADDR と SO_EXCLUSIVEADDRUSE](https://learn.microsoft.com/en-us/windows/win32/winsock/using-so-reuseaddr-and-so-exclusiveaddruse)
- [Chromium: new-window スイッチの定義](https://chromium.googlesource.com/chromium/src/+/c60e98e3f568d5dd5fc158652ba264d3d6647393/chrome/common/chrome_switches.cc)
