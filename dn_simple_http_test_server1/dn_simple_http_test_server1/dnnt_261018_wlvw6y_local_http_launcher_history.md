###### ============================================================

# 1. 2026/10/18 21:39:44 (JST) / 今回の R01

**DNNT 261018_WLVW6Y ローカルホスト用軽量 HTTP サーバー + Chrome ランチャー dn_simple_http_test_server1**

ローカルHTMLの試験に使う静的HTTPサーバーとChromeランチャーを、新規に単一C#ソースとして実装した。Windows向け .NET Framework 4.8 / C# 7.3 / AnyCPU のコンソールアプリケーションであり、追加NuGet依存はない。ソース、ソリューション、プロジェクト、ビルド済みEXE、設定、原仕様、試験スクリプト、試験記録をZIPに同梱する。

実際のソケットでHTTPを試験し、最終結果は **PASS 1438 / FAIL 0 / SKIP 1**。300秒のタイムアウトも短縮せず確認した。

## 1-2. 【警告: 曖昧な仕様】と採用した解釈

1. **初版番号の変数が「まだない」**: `VAR_NEW_VERSION=R01` を今回の初版番号とし、ファイル名は `r01_261018_wlvw6y` とした。原変数は下記2節にそのまま残す。
2. **WinExe とコンソール指定**: 個別R01仕様のコンソール表示・キー操作を優先し、プロジェクトを `OutputType=Exe` とした。Windows CUI/AnyCPUもPEで確認した。
3. **HTTP/0.9と固定ヘッダ**: HTTP/0.9の本文のみの形式を優先し、固定8ヘッダはHTTP/1.0・1.1へ付ける。1xx/204にはContent-Lengthを付けず、1xx/204/304には本文を付けない。304のContent-Lengthは省略する。8ヘッダ自体は100応答にも付ける。
4. **MIMEの重複**: 1,890行を記載順に適用し、大小文字を区別しない1,296拡張子に集約する。594行が再登場し、そのうち87行で値が変わる。末尾の特別上書きも後勝ちで反映する。最長の既知拡張子を優先するので `.dll.config` / `.exe.config` は `text/xml`、一般の `.config` は `text/plain;charset=UTF-8`。
5. **nolingerと即再利用**: `LingerOption(false,0)` を全socketに明示する。WindowsのSO_REUSEADDRは別プロセスの二重bindも許し得るため待受けは排他bindとし、LinuxはSO_REUSEADDRを使う。NoLingerはTCP状態を消す保証ではないため、Windowsが再bindを拒んだ場合は次の候補へ進む。「必ず同じポートを即時再利用」という絶対保証は実装しない。この実用上の限界はREADMEにも明示した。
6. **いかなるプロトコルのlisten**: ポートを持つTCP listenerとUDP bound endpointを、loopback以外やwildcardも含めて調べる。UDPはTCPのLISTEN状態を持たず、raw IP/ICMPには同じ意味のポート番号がない。
7. **1000回くらい**: 7101～8100の1000候補を対象とする。API列挙で使用中の候補はbindせず除外し、両loopbackに成功する最小候補を保持する。
8. **標準的HTTPの範囲**: 静的配信に必要なHTTP/0.9～1.1、GET/HEAD/OPTIONS、framing、接続管理、条件付き取得とbyte Rangeを扱う。16KiB/行、64KiB/ヘッダ、100フィールド、64MiB/要求本文、16指定/Rangeの資源上限を採用した。配信ファイル全体のサイズはこの64MiB制限の対象ではない。
9. **弱validatorとIf-Range**: 低遅延のため、大きいファイルを全走査して強いETagを作る処理は採用しない。ファイルはsize/mtimeの弱ETagとし、If-Rangeの強比較は成立扱いにせず、日時指定でもRangeを無視する。通常取得では全体200、先行する条件付き要求が成立すれば304/412などが優先される。単独Rangeは206。
10. **Linux実行**: Windows固有のChrome探索・起動は動的OS分岐で省略する。stdinがEOFになってもサーバーを続け、利用不能な操作プロンプトの再描画は停止する。物理的にIPv4/IPv6の双方を利用できない環境では、片方だけで開始せず例外で終了する。
11. **見出しのR1**: 入力末尾の `[R1.specs]` 表記は開始側の `[R01.specs]` と同じ範囲の終端と解釈した。

# 2. 今回の変数（入力どおり）

```text
VAR_PROJECT_ID: 初版の DNNT 番号: 261018_WLVW6Y
VAR_FIRST_VERSION: 初版のバージョン (R は、リビジョンを意味する): まだない
VAR_OLD_VERSION: 添付されているソースコードのバージョン (R は、リビジョンを意味する): まだない
VAR_NEW_VERSION: この度作成したいバージョン (R は、リビジョンを意味する): R01
```

# 3. 生成AI・日時・入力

- 生成AI: OpenAI ChatGPT / GPT-6 Astra Pro。
- バージョン: GPT-6系列。環境から詳細snapshot識別子は取得できないため推測していない。
- 思考レベル: 環境から取得できる明示値がないため未記載。
- セッション開始日時: 2026/10/18 20:57:19 (JST)。依頼メッセージの受信時刻を使用した。
- 初回の応答用ソース生成日時: 2026/10/18 21:20:45 (JST)。
- 本履歴・応答成果物の生成日時: 2026/10/18 21:39:44 (JST)。
- 実質的なユーザー入力: 添付 `貼り付けたMarkdown(5).md` の本文。次の4節に原文全体を転記し、`specification.md` に原ファイルのバイト列も保存した。実装に無関係な環境通知や過去会話は転記対象にしていない。
- 主ソース: `dnnt_261018_wlvw6y_local_http_launcher_r01_261018_wlvw6y.cs`。ソース先頭のDNNT行は入力の1行目と同じ。

# 4. 入力プロンプト（R01仕様・MIME表・CODE_RULE・TASK_INSTRUCTIONを含む原文全体）

以下は改変せずに転記した入力の本文である。Markdownのエスケープ表現も原文のまま保持する。

````text
DNNT 261018_WLVW6Y ローカルホスト用軽量 HTTP サーバー + Chrome ランチャー dn_simple_http_test_server1

\==================



0\. 変数



VAR_PROJECT_ID: 初版の DNNT 番号: 261018_WLVW6Y

VAR_FIRST_VERSION: 初版のバージョン (R は、リビジョンを意味する): まだない

VAR_OLD_VERSION: 添付されているソースコードのバージョン (R は、リビジョンを意味する): まだない

VAR_NEW_VERSION: この度作成したいバージョン (R は、リビジョンを意味する): R01



1\. タスク

下記の [TASK_INSTRUCTION] を実行せよ。

また、AI は、本タスクを実行して実装した C# プログラムを実際に HTTP サーバとして動作するかどうか実際的にテストせよ (Chrome の起動部分のコードのテストは不要。テストは、http サーバ機能のみで足りる)。この際、[CODE_RULE] 記載の .NET Framework 4.8 は Windows 環境でしかテストできないので、AI が望むテスト環境で動作する他の互換性のある .NET ランタイムを用いて同じソースコードでテストせよ (従って、本プログラミングタスクにおいては、そのようなテスト用の .NET 互換ランタイムでも動作するような c# プログラムを書く、という制約が追加される)。Linux 環境でテストする際は、仕様に記載のある、Windows 固有の、netstat 用 API やレジストリなどの部分はテスト不要。プログラムは Linux で動作している場合 / Windows で動作している場合とで実行時動的条件分岐でこれらの課題に対応。





\--- [R01.specs] ここから ---



プログラム名: dn_simple_http_test_server1



目的: localhost デバッグで利用できるとても軽量な静的 HTTP サーバー、およびその HTTP サーバ機能動作開始後に自動的に Windows 上で Chrome を起動してそのコンテンツを表示。HTTP 0.9, 1.0, 1.1 に対応。



1\. プログラムの起動



(1) Windows デスクトップ用コンソールアプリケーションとする。

コマンドライン引数として、物理的なディレクトリまたはファイルのフルパス (C:\tmp\a.html のようなパスあるいは \\\server\share\a.html のような UNC パスのいずれか) の指定を受ける。なお、Linux 上でテストする場合は、Linux のファイルのフルパスの指定でもよい。



もし指定がされていない場合は、コマンドラインに、使い方を書いて終了する。



コマンドライン引数には 1 個のフルパスを指定する。



場合分け:



(t1) コマンドライン引数に、ファイルが指定された場合、そのファイルが置かれている親ディレクトリが、本プログラムで実装する HTTP サーバの「/」 に相当する。

(t2) コマンドライン引数に、ディレクトリが指定された場合は、そのディレクトリが、本プログラムで実装する HTTP サーバの「/」 に相当する。



(2) 内部で TCP ソケットを用いた軽量の HTTP サーバを実装する。TCP ソケットは listen または bind する際に nolinger (そのプロセスがそのポートを解放したら直ちにそのポートの再利用が可能であること) をセットする。またすべてのソケットは nagle アルゴリズムを OFF にする。



(3) ポート番号の決定方法

HTTP サーバの bind 対象は 127.0.0.1 (IPv4) および ::1 (IPv6) の 2 つの localhost とする。ポート番号は、内部的にまず windows の netstat の結果を得るのと同様の API を用いて、Windows の他のプロセスが listen していない (127.0.0.1 (IPv4) および ::1 (IPv6) 以外も含めて、いかなるソケットアドレスにおいても、いかなるプロトコルでも listen していない) ポートをまず当たりをつける ---- [a]。次に、7101 番から順に、前記 [a] で listen されていないポートについて、IPv4 および IPv6 で 127.0.0.1 (IPv4) および ::1 (IPv6) として bind / listen を試み、v4 と v6 の「両方」でポート番号の確保に成功した最小のポート番号を、占有し続ける。



この処理が、1000 回くらい試しても失敗するなら、例外を吐いて終了する。



(4) コンソール画面の表示

以下を表示する。

\--- ここから ---

dn_simple_http_test_server1: TCP ポート番号 XXXX で Listen 中

http\://127.0.0.1:【ポート番号をここに書く】/【コマンドライン引数に .html のようなファイルが指定されたならば、その html ファイル名の HTTP 上の絶対パス】

\--- ここまで ---



(5) Web ブラウザの起動

HTTP サーバの動作が開始されたならば、Web ブラウザとして、PC 上に現在インストールされている PC 版 Google Chrome において、

\- 上記 (t1) の場合は、 http\://127.0.0.1:【ポート番号をここに書く】/【コマンドライン引数に .html のようなファイルが指定されたならば、その html ファイル名の HTTP 上の絶対パス】 を起動する。

\- 上記 (t2) の場合は、 http\://127.0.0.1:【ポート番号をここに書く】/ を起動する。

chrome の exe のパスは、レジストリなどで取得する。



この際、できるだけ、新規ウインドウで開くようにする。(既存ウインドウのタブではなく新規ウインドウ。ただしデバッグモードなどではなく通常のモードとする)



Web ブラウザを起動したら、コンソール画面上に、その旨の案内を出す。



(6) その後のコンソール

コンソールには、

「q: 終了、e または o: Web ブラウザでページを再度開く?>」

というプロンプトを常に出す。q を押すとプロセスはただちに強制終了する (クリーンアップ処理は不要)。e または o を押すと、(5) と同じ処理が再度行なわれる。

HTTP のレイヤでリクエストに対するエラー (not found など) が発生したら、その旨をコンソールに出す。



2\. Web サーバーの仕様



(11) 特に高速で低遅延で動作すること。多数のリクエストを同時に捌けること。



(12) index.html 的なファイル名は

&#x20;   \- index.html

&#x20;   \- index.htm

&#x20;   \- default.html

&#x20;   \- default.htm

&#x20;   の決め打ちとする。



(13) TCP のレベルでの送受信タイムアウトは 300 秒とし、それを超えて無通信である場合は、切断する。特に切断の場合の表示は不要。



(14) HTTP 0.9 ～ 1.1 の標準的機能やプロトコルに対応すること。メソッドは GET / HEAD / OPTIONS のみでよい。Range リクエスト対応必須。



(15) 圧縮機能は不要。



(16) CGI のような機能は不要。静的ファイルのみ。



(17) ディレクトリリスティング機能は必要。(12) がない場合は Apache や nginx に似たディレクトリリスティングをせよ。



(18) ディレクトリトラバーサルは禁止。ただし、シンボリックリンクやジャンクションは特段制約なく辿ってよい。



(19) MIME TYPE の応答は、[T261018_EL_VUFEJ_33] の内容をプログラム中にハードコートしこれにより行なう。登録拡張子がない場合は「application/octet-stream」とする。



(20) 自作 html アプリのデバッグに必要なため、[T261018_EL_VURAH_35] のヘッダを常に応答すること。





\--- [T261018_EL_VURAH_35] ここから ---

Access-Control-Allow-Origin: \*

Access-Control-Allow-Methods: GET, HEAD, OPTIONS

Access-Control-Allow-Headers: \*

Access-Control-Max-Age: 86400

Cross-Origin-Resource-Policy: cross-origin

Cross-Origin-Opener-Policy: same-origin

Cross-Origin-Embedder-Policy: require-corp

Cache-Control: no-store

\--- [T261018_EL_VURAH_35] ここまで ---













\--- [T261018_EL_VUFEJ_33] ここから ---

\# From https\://gist.github.com/PawelGerr/cb5886ee26c41ae2386b



.123 application/vnd.lotus-1-2-3

.323 text/h323

.3dml text/vnd.in3d.3dml

.3ds image/x-3ds

.3g2 video/3gpp2

.3gp video/3gpp

.3gp2 video/3gpp2

.3gpp video/3gpp

.7z application/x-7z-compressed

.aa audio/audible

.aab application/x-authorware-bin

.aac audio/x-aac

.aaf application/octet-stream

.aam application/x-authorware-map

.aas application/x-authorware-seg

.aax audio/vnd.audible.aax

.abw application/x-abiword

.ac application/pkix-attr-cert

.ac3 audio/ac3

.aca application/octet-stream

.acc application/vnd.americandynamics.acc

.accda application/msaccess.addin

.accdb application/msaccess

.accdc application/msaccess.cab

.accde application/msaccess

.accdr application/msaccess.runtime

.accdt application/msaccess

.accdw application/msaccess.webapplication

.accft application/msaccess.ftemplate

.ace application/x-ace-compressed

.acu application/vnd.acucobol

.acutc application/vnd.acucorp

.acx application/internet-property-stream

.AddIn text/xml

.ade application/msaccess

.adobebridge application/x-bridge-url

.adp audio/adpcm

.ADT audio/vnd.dlna.adts

.ADTS audio/aac

.aep application/vnd.audiograph

.afm application/x-font-type1

.afp application/vnd.ibm.modcap

.ahead application/vnd.ahead.space

.ai application/postscript

.aif audio/x-aiff

.aifc audio/x-aiff

.aiff audio/x-aiff

.air application/vnd.adobe.air-application-installer-package+zip

.ait application/vnd.dvb.ait

.amc application/x-mpeg

.ami application/vnd.amiga.ami

.apk application/vnd.android.package-archive

.appcache text/cache-manifest

.application application/x-ms-application

.apr application/vnd.lotus-approach

.arc application/x-freearc

.art image/x-jg

.asa application/xml

.asax application/xml

.asc application/pgp-signature

.ascx application/xml

.asd application/octet-stream

.asf video/x-ms-asf

.ashx application/xml

.asi application/octet-stream

.asm text/x-asm

.asmx application/xml

.aso application/vnd.accpac.simply.aso

.aspx application/xml

.asr video/x-ms-asf

.asx video/x-ms-asf

.atc application/vnd.acucorp

.atom application/atom+xml

.atomcat application/atomcat+xml

.atomsvc application/atomsvc+xml

.atx application/vnd.antix.game-component

.au audio/basic

.avi video/x-msvideo

.aw application/applixware

.axs application/olescript

.azf application/vnd.airzip.filesecure.azf

.azs application/vnd.airzip.filesecure.azs

.azw application/vnd.amazon.ebook

.bas text/plain

.bat application/x-msdownload

.bcpio application/x-bcpio

.bdf application/x-font-bdf

.bdm application/vnd.syncml.dm+wbxml

.bed application/vnd.realvnc.bed

.bh2 application/vnd.fujitsu.oasysprs

.bin application/octet-stream

.blb application/x-blorb

.blorb application/x-blorb

.bmi application/vnd.bmi

.bmp image/bmp

.book application/vnd.framemaker

.box application/vnd.previewsystems.box

.boz application/x-bzip2

.bpk application/octet-stream

.btif image/prs.btif

.bz application/x-bzip

.bz2 application/x-bzip2

.c text/x-c

.c11amc application/vnd.cluetrust.cartomobile-config

.c11amz application/vnd.cluetrust.cartomobile-config-pkg

.c4d application/vnd.clonk.c4group

.c4f application/vnd.clonk.c4group

.c4g application/vnd.clonk.c4group

.c4p application/vnd.clonk.c4group

.c4u application/vnd.clonk.c4group

.cab application/vnd.ms-cab-compressed

.caf audio/x-caf

.calx application/vnd.ms-office.calx

.cap application/vnd.tcpdump.pcap

.car application/vnd.curl.car

.cat application/vnd.ms-pki.seccat

.cb7 application/x-cbr

.cba application/x-cbr

.cbr application/x-cbr

.cbt application/x-cbr

.cbz application/x-cbr

.cc text/x-c

.cct application/x-director

.ccxml application/ccxml+xml

.cd text/plain

.cdbcmsg application/vnd.contact.cmsg

.cdda audio/aiff

.cdf application/x-netcdf

.cdkey application/vnd.mediastation.cdkey

.cdmia application/cdmi-capability

.cdmic application/cdmi-container

.cdmid application/cdmi-domain

.cdmio application/cdmi-object

.cdmiq application/cdmi-queue

.cdx chemical/x-cdx

.cdxml application/vnd.chemdraw+xml

.cdy application/vnd.cinderella

.cer application/pkix-cert

.cfs application/x-cfs-compressed

.cgm image/cgm

.chat application/x-chat

.chm application/vnd.ms-htmlhelp

.chrt application/vnd.kde.kchart

.cif chemical/x-cif

.cii application/vnd.anser-web-certificate-issue-initiation

.cil application/vnd.ms-artgalry

.cla application/vnd.claymore

.class application/java-vm

.clkk application/vnd.crick.clicker.keyboard

.clkp application/vnd.crick.clicker.palette

.clkt application/vnd.crick.clicker.template

.clkw application/vnd.crick.clicker.wordbank

.clkx application/vnd.crick.clicker

.clp application/x-msclip

.cmc application/vnd.cosmocaller

.cmdf chemical/x-cmdf

.cml chemical/x-cml

.cmp application/vnd.yellowriver-custom-menu

.cmx image/x-cmx

.cnf text/plain

.cod application/vnd.rim.cod

.com application/x-msdownload

.conf text/plain

.config application/xml

.contact text/x-ms-contact

.coverage application/xml

.cpio application/x-cpio

.cpp text/x-c

.cpt application/mac-compactpro

.crd application/x-mscardfile

.crl application/pkix-crl

.crt application/x-x509-ca-cert

.cryptonote application/vnd.rig.cryptonote

.cs text/plain

.csdproj text/plain

.csh application/x-csh

.csml chemical/x-csml

.csp application/vnd.commonspace

.csproj text/plain

.css text/css

.cst application/x-director

.csv text/csv

.cu application/cu-seeme

.cur application/octet-stream

.curl text/vnd.curl

.cww application/prs.cww

.cxt application/x-director

.cxx text/x-c

.dae model/vnd.collada+xml

.daf application/vnd.mobius.daf

.dart application/vnd.dart

.dat application/octet-stream

.dataless application/vnd.fdsn.seed

.datasource application/xml

.davmount application/davmount+xml

.dbk application/docbook+xml

.dbproj text/plain

.dcr application/x-director

.dcurl text/vnd.curl.dcurl

.dd2 application/vnd.oma.dd2+xml

.ddd application/vnd.fujixerox.ddd

.deb application/x-debian-package

.def text/plain

.deploy application/octet-stream

.der application/x-x509-ca-cert

.dfac application/vnd.dreamfactory

.dgc application/x-dgc-compressed

.dgml application/xml

.dib image/bmp

.dic text/x-c

.dif video/x-dv

.dir application/x-director

.dis application/vnd.mobius.dis

.disco text/xml

.dist application/octet-stream

.distz application/octet-stream

.djv image/vnd.djvu

.djvu image/vnd.djvu

.dll application/x-msdownload

.dll.config text/xml

.dlm text/dlm

.dmg application/x-apple-diskimage

.dmp application/vnd.tcpdump.pcap

.dms application/octet-stream

.dna application/vnd.dna

.doc application/msword

.docm application/vnd.ms-word.document.macroenabled.12

.docx application/vnd.openxmlformats-officedocument.wordprocessingml.document

.dot application/msword

.dotm application/vnd.ms-word.template.macroenabled.12

.dotx application/vnd.openxmlformats-officedocument.wordprocessingml.template

.dp application/vnd.osgi.dp

.dpg application/vnd.dpgraph

.dra audio/vnd.dra

.dsc text/prs.lines.tag

.dsp application/octet-stream

.dssc application/dssc+der

.dsw text/plain

.dtb application/x-dtbook+xml

.dtd application/xml-dtd

.dts audio/vnd.dts

.dtsConfig text/xml

.dtshd audio/vnd.dts.hd

.dump application/octet-stream

.dv video/x-dv

.dvb video/vnd.dvb.file

.dvi application/x-dvi

.dwf model/vnd.dwf

.dwg image/vnd.dwg

.dwp application/octet-stream

.dxf image/vnd.dxf

.dxp application/vnd.spotfire.dxp

.dxr application/x-director

.ecelp4800 audio/vnd.nuera.ecelp4800

.ecelp7470 audio/vnd.nuera.ecelp7470

.ecelp9600 audio/vnd.nuera.ecelp9600

.ecma application/ecmascript

.edm application/vnd.novadigm.edm

.edx application/vnd.novadigm.edx

.efif application/vnd.picsel

.ei6 application/vnd.pg.osasli

.elc application/octet-stream

.emf application/x-msmetafile

.eml message/rfc822

.emma application/emma+xml

.emz application/x-msmetafile

.eol audio/vnd.digital-winds

.eot application/vnd.ms-fontobject

.eps application/postscript

.epub application/epub+zip

.es3 application/vnd.eszigno3+xml

.esa application/vnd.osgi.subsystem

.esf application/vnd.epson.esf

.et3 application/vnd.eszigno3+xml

.etl application/etl

.etx text/x-setext

.eva application/x-eva

.evy application/x-envoy

.exe application/x-msdownload

.exe.config text/xml

.exi application/exi

.ext application/vnd.novadigm.ext

.ez application/andrew-inset

.ez2 application/vnd.ezpix-album

.ez3 application/vnd.ezpix-package

.f text/x-fortran

.f4v video/x-f4v

.f77 text/x-fortran

.f90 text/x-fortran

.fbs image/vnd.fastbidsheet

.fcdt application/vnd.adobe.formscentral.fcdt

.fcs application/vnd.isac.fcs

.fdf application/vnd.fdf

.fe_launch application/vnd.denovo.fcselayout-link

.fg5 application/vnd.fujitsu.oasysgp

.fgd application/x-director

.fh image/x-freehand

.fh4 image/x-freehand

.fh5 image/x-freehand

.fh7 image/x-freehand

.fhc image/x-freehand

.fif application/fractals

.fig application/x-xfig

.filters Application/xml

.fla application/octet-stream

.flac audio/x-flac

.fli video/x-fli

.flo application/vnd.micrografx.flo

.flr x-world/x-vrml

.flv video/x-flv

.flw application/vnd.kde.kivio

.flx text/vnd.fmi.flexstor

.fly text/vnd.fly

.fm application/vnd.framemaker

.fnc application/vnd.frogans.fnc

.for text/x-fortran

.fpx image/vnd.fpx

.frame application/vnd.framemaker

.fsc application/vnd.fsc.weblaunch

.fsscript application/fsharp-script

.fst image/vnd.fst

.fsx application/fsharp-script

.ftc application/vnd.fluxtime.clip

.fti application/vnd.anser-web-funds-transfer-initiation

.fvt video/vnd.fvt

.fxp application/vnd.adobe.fxp

.fxpl application/vnd.adobe.fxp

.fzs application/vnd.fuzzysheet

.g2w application/vnd.geoplan

.g3 image/g3fax

.g3w application/vnd.geospace

.gac application/vnd.groove-account

.gam application/x-tads

.gbr application/rpki-ghostbusters

.gca application/x-gca-compressed

.gdl model/vnd.gdl

.generictest application/xml

.geo application/vnd.dynageo

.gex application/vnd.geometry-explorer

.ggb application/vnd.geogebra.file

.ggt application/vnd.geogebra.tool

.ghf application/vnd.groove-help

.gif image/gif

.gim application/vnd.groove-identity-message

.gml application/gml+xml

.gmx application/vnd.gmx

.gnumeric application/x-gnumeric

.gph application/vnd.flographit

.gpx application/gpx+xml

.gqf application/vnd.grafeq

.gqs application/vnd.grafeq

.gram application/srgs

.gramps application/x-gramps-xml

.gre application/vnd.geometry-explorer

.group text/x-ms-group

.grv application/vnd.groove-injector

.grxml application/srgs+xml

.gsf application/x-font-ghostscript

.gsm audio/x-gsm

.gtar application/x-gtar

.gtm application/vnd.groove-tool-message

.gtw model/vnd.gtw

.gv text/vnd.graphviz

.gxf application/gxf

.gxt application/vnd.geonext

.gz application/x-gzip

.h text/x-c

.h261 video/h261

.h263 video/h263

.h264 video/h264

.hal application/vnd.hal+xml

.hbci application/vnd.hbci

.hdf application/x-hdf

.hdml text/x-hdml

.hh text/x-c

.hhc application/x-oleobject

.hhk application/octet-stream

.hhp application/octet-stream

.hlp application/winhlp

.hpgl application/vnd.hp-hpgl

.hpid application/vnd.hp-hpid

.hpp text/plain

.hps application/vnd.hp-hps

.hqx application/mac-binhex40

.hta application/hta

.htc text/x-component

.htke application/vnd.kenameaapp

.htm text/html

.html text/html

.htt text/webviewhtml

.hvd application/vnd.yamaha.hv-dic

.hvp application/vnd.yamaha.hv-voice

.hvs application/vnd.yamaha.hv-script

.hxa application/xml

.hxc application/xml

.hxd application/octet-stream

.hxe application/xml

.hxf application/xml

.hxh application/octet-stream

.hxi application/octet-stream

.hxk application/xml

.hxq application/octet-stream

.hxr application/octet-stream

.hxs application/octet-stream

.hxt text/html

.hxv application/xml

.hxw application/octet-stream

.hxx text/plain

.i text/plain

.i2g application/vnd.intergeo

.icc application/vnd.iccprofile

.ice x-conference/x-cooltalk

.icm application/vnd.iccprofile

.ico image/x-icon

.ics text/calendar

.idl text/plain

.ief image/ief

.ifb text/calendar

.ifm application/vnd.shana.informed.formdata

.iges model/iges

.igl application/vnd.igloader

.igm application/vnd.insors.igm

.igs model/iges

.igx application/vnd.micrografx.igx

.iif application/vnd.shana.informed.interchange

.iii application/x-iphone

.imp application/vnd.accpac.simply.imp

.ims application/vnd.ms-ims

.in text/plain

.inc text/plain

.inf application/octet-stream

.ink application/inkml+xml

.inkml application/inkml+xml

.inl text/plain

.ins application/x-internet-signup

.install application/x-install-instructions

.iota application/vnd.astraea-software.iota

.ipa application/x-itunes-ipa

.ipfix application/ipfix

.ipg application/x-itunes-ipg

.ipk application/vnd.shana.informed.package

.ipproj text/plain

.ipsw application/x-itunes-ipsw

.iqy text/x-ms-iqy

.irm application/vnd.ibm.rights-management

.irp application/vnd.irepository.package+xml

.iso application/x-iso9660-image

.isp application/x-internet-signup

.ite application/x-itunes-ite

.itlp application/x-itunes-itlp

.itms application/x-itunes-itms

.itp application/vnd.shana.informed.formtemplate

.itpc application/x-itunes-itpc

.IVF video/x-ivf

.ivp application/vnd.immervision-ivp

.ivu application/vnd.immervision-ivu

.jad text/vnd.sun.j2me.app-descriptor

.jam application/vnd.jam

.jar application/java-archive

.java text/x-java-source

.jck application/liquidmotion

.jcz application/liquidmotion

.jfif image/pjpeg

.jisp application/vnd.jisp

.jlt application/vnd.hp-jlyt

.jnlp application/x-java-jnlp-file

.joda application/vnd.joost.joda-archive

.jpb application/octet-stream

.jpe image/jpeg

.jpeg image/jpeg

.jpg image/jpeg

.jpgm video/jpm

.jpgv video/jpeg

.jpm video/jpm

.js application/javascript

.json application/json

.jsonml application/jsonml+json

.jsx text/jscript

.jsxbin text/plain

.kar audio/midi

.karbon application/vnd.kde.karbon

.kfo application/vnd.kde.kformula

.kia application/vnd.kidspiration

.kml application/vnd.google-earth.kml+xml

.kmz application/vnd.google-earth.kmz

.kne application/vnd.kinar

.knp application/vnd.kinar

.kon application/vnd.kde.kontour

.kpr application/vnd.kde.kpresenter

.kpt application/vnd.kde.kpresenter

.kpxx application/vnd.ds-keypoint

.ksp application/vnd.kde.kspread

.ktr application/vnd.kahootz

.ktx image/ktx

.ktz application/vnd.kahootz

.kwd application/vnd.kde.kword

.kwt application/vnd.kde.kword

.lasxml application/vnd.las.las+xml

.latex application/x-latex

.lbd application/vnd.llamagraphics.life-balance.desktop

.lbe application/vnd.llamagraphics.life-balance.exchange+xml

.les application/vnd.hhe.lesson-player

.lha application/x-lzh-compressed

.library-ms application/windows-library+xml

.link66 application/vnd.route66.link66+xml

.list text/plain

.list3820 application/vnd.ibm.modcap

.listafp application/vnd.ibm.modcap

.lit application/x-ms-reader

.lnk application/x-ms-shortcut

.loadtest application/xml

.log text/plain

.lostxml application/lost+xml

.lpk application/octet-stream

.lrf application/octet-stream

.lrm application/vnd.ms-lrm

.lsf video/x-la-asf

.lst text/plain

.lsx video/x-la-asf

.ltf application/vnd.frogans.ltf

.lvp audio/vnd.lucent.voice

.lwp application/vnd.lotus-wordpro

.lzh application/x-lzh-compressed

.m13 application/x-msmediaview

.m14 application/x-msmediaview

.m1v video/mpeg

.m21 application/mp21

.m2a audio/mpeg

.m2t video/vnd.dlna.mpeg-tts

.m2ts video/vnd.dlna.mpeg-tts

.m2v video/mpeg

.m3a audio/mpeg

.m3u audio/x-mpegurl

.m3u8 application/vnd.apple.mpegurl

.m4a audio/m4a

.m4b audio/m4b

.m4p audio/m4p

.m4r audio/x-m4r

.m4u video/vnd.mpegurl

.m4v video/x-m4v

.ma application/mathematica

.mac image/x-macpaint

.mads application/mads+xml

.mag application/vnd.ecowin.chart

.mak text/plain

.maker application/vnd.framemaker

.man text/troff

.manifest application/x-ms-manifest

.map text/plain

.mar application/octet-stream

.master application/xml

.mathml application/mathml+xml

.mb application/mathematica

.mbk application/vnd.mobius.mbk

.mbox application/mbox

.mc1 application/vnd.medcalcdata

.mcd application/vnd.mcd

.mcurl text/vnd.curl.mcurl

.mda application/msaccess

.mdb application/x-msaccess

.mde application/msaccess

.mdi image/vnd.ms-modi

.mdp application/octet-stream

.me text/troff

.mesh model/mesh

.meta4 application/metalink4+xml

.metalink application/metalink+xml

.mets application/mets+xml

.mfm application/vnd.mfmp

.mfp application/x-shockwave-flash

.mft application/rpki-manifest

.mgp application/vnd.osgeo.mapguide.package

.mgz application/vnd.proteus.magazine

.mht message/rfc822

.mhtml message/rfc822

.mid audio/midi

.midi audio/midi

.mie application/x-mie

.mif application/vnd.mif

.mime message/rfc822

.mix application/octet-stream

.mj2 video/mj2

.mjp2 video/mj2

.mk text/plain

.mk3d video/x-matroska

.mka audio/x-matroska

.mks video/x-matroska

.mkv video/x-matroska

.mlp application/vnd.dolby.mlp

.mmd application/vnd.chipnuts.karaoke-mmd

.mmf application/vnd.smaf

.mmr image/vnd.fujixerox.edmics-mmr

.mng video/x-mng

.mno text/xml

.mny application/x-msmoney

.mobi application/x-mobipocket-ebook

.mod video/mpeg

.mods application/mods+xml

.mov video/quicktime

.movie video/x-sgi-movie

.mp2 audio/mpeg

.mp21 application/mp21

.mp2a audio/mpeg

.mp2v video/mpeg

.mp3 audio/mpeg

.mp4 video/mp4

.mp4a audio/mp4

.mp4s application/mp4

.mp4v video/mp4

.mpa video/mpeg

.mpc application/vnd.mophun.certificate

.mpe video/mpeg

.mpeg video/mpeg

.mpf application/vnd.ms-mediapackage

.mpg video/mpeg

.mpg4 video/mp4

.mpga audio/mpeg

.mpkg application/vnd.apple.installer+xml

.mpm application/vnd.blueice.multipass

.mpn application/vnd.mophun.application

.mpp application/vnd.ms-project

.mpt application/vnd.ms-project

.mpv2 video/mpeg

.mpy application/vnd.ibm.minipay

.mqv video/quicktime

.mqy application/vnd.mobius.mqy

.mrc application/marc

.mrcx application/marcxml+xml

.ms text/troff

.mscml application/mediaservercontrol+xml

.mseed application/vnd.fdsn.mseed

.mseq application/vnd.mseq

.msf application/vnd.epson.msf

.msh model/mesh

.msi application/x-msdownload

.msl application/vnd.mobius.msl

.mso application/octet-stream

.msty application/vnd.muvee.style

.mts model/vnd.mts

.mtx application/xml

.mus application/vnd.musician

.musicxml application/vnd.recordare.musicxml+xml

.mvb application/x-msmediaview

.mvc application/x-miva-compiled

.mwf application/vnd.mfer

.mxf application/mxf

.mxl application/vnd.recordare.musicxml

.mxml application/xv+xml

.mxp application/x-mmxp

.mxs application/vnd.triscape.mxs

.mxu video/vnd.mpegurl

.n3 text/n3

.nb application/mathematica

.nbp application/vnd.wolfram.player

.nc application/x-netcdf

.ncx application/x-dtbncx+xml

.nfo text/x-nfo

.n-gage application/vnd.nokia.n-gage.symbian.install

.ngdat application/vnd.nokia.n-gage.data

.nitf application/vnd.nitf

.nlu application/vnd.neurolanguage.nlu

.nml application/vnd.enliven

.nnd application/vnd.noblenet-directory

.nns application/vnd.noblenet-sealer

.nnw application/vnd.noblenet-web

.npx image/vnd.net-fpx

.nsc application/x-conference

.nsf application/vnd.lotus-notes

.ntf application/vnd.nitf

.nws message/rfc822

.nzb application/x-nzb

.oa2 application/vnd.fujitsu.oasys2

.oa3 application/vnd.fujitsu.oasys3

.oas application/vnd.fujitsu.oasys

.obd application/x-msbinder

.obj application/x-tgif

.ocx application/octet-stream

.oda application/oda

.odb application/vnd.oasis.opendocument.database

.odc application/vnd.oasis.opendocument.chart

.odf application/vnd.oasis.opendocument.formula

.odft application/vnd.oasis.opendocument.formula-template

.odg application/vnd.oasis.opendocument.graphics

.odh text/plain

.odi application/vnd.oasis.opendocument.image

.odl text/plain

.odm application/vnd.oasis.opendocument.text-master

.odp application/vnd.oasis.opendocument.presentation

.ods application/vnd.oasis.opendocument.spreadsheet

.odt application/vnd.oasis.opendocument.text

.oga audio/ogg

.ogg audio/ogg

.ogv video/ogg

.ogx application/ogg

.omdoc application/omdoc+xml

.one application/onenote

.onea application/onenote

.onepkg application/onenote

.onetmp application/onenote

.onetoc application/onenote

.onetoc2 application/onenote

.opf application/oebps-package+xml

.opml text/x-opml

.oprc application/vnd.palm

.orderedtest application/xml

.org application/vnd.lotus-organizer

.osdx application/opensearchdescription+xml

.osf application/vnd.yamaha.openscoreformat

.osfpvg application/vnd.yamaha.openscoreformat.osfpvg+xml

.otc application/vnd.oasis.opendocument.chart-template

.otf application/x-font-otf

.otg application/vnd.oasis.opendocument.graphics-template

.oth application/vnd.oasis.opendocument.text-web

.oti application/vnd.oasis.opendocument.image-template

.otp application/vnd.oasis.opendocument.presentation-template

.ots application/vnd.oasis.opendocument.spreadsheet-template

.ott application/vnd.oasis.opendocument.text-template

.oxps application/oxps

.oxt application/vnd.openofficeorg.extension

.p text/x-pascal

.p10 application/pkcs10

.p12 application/x-pkcs12

.p7b application/x-pkcs7-certificates

.p7c application/pkcs7-mime

.p7m application/pkcs7-mime

.p7r application/x-pkcs7-certreqresp

.p7s application/pkcs7-signature

.p8 application/pkcs8

.pas text/x-pascal

.paw application/vnd.pawaafile

.pbd application/vnd.powerbuilder6

.pbm image/x-portable-bitmap

.pcap application/vnd.tcpdump.pcap

.pcast application/x-podcast

.pcf application/x-font-pcf

.pcl application/vnd.hp-pcl

.pclxl application/vnd.hp-pclxl

.pct image/x-pict

.pcurl application/vnd.curl.pcurl

.pcx image/x-pcx

.pcz application/octet-stream

.pdb application/vnd.palm

.pdf application/pdf

.pfa application/x-font-type1

.pfb application/x-font-type1

.pfm application/x-font-type1

.pfr application/font-tdpfr

.pfx application/x-pkcs12

.pgm image/x-portable-graymap

.pgn application/x-chess-pgn

.pgp application/pgp-encrypted

.pic image/x-pict

.pict image/pict

.pkg application/octet-stream

.pkgdef text/plain

.pkgundef text/plain

.pki application/pkixcmp

.pkipath application/pkix-pkipath

.pko application/vnd.ms-pki.pko

.plb application/vnd.3gpp.pic-bw-large

.plc application/vnd.mobius.plc

.plf application/vnd.pocketlearn

.pls application/pls+xml

.pma application/x-perfmon

.pmc application/x-perfmon

.pml application/vnd.ctc-posml

.pmr application/x-perfmon

.pmw application/x-perfmon

.png image/png

.pnm image/x-portable-anymap

.pnt image/x-macpaint

.pntg image/x-macpaint

.pnz image/png

.portpkg application/vnd.macports.portpkg

.pot application/vnd.ms-powerpoint

.potm application/vnd.ms-powerpoint.template.macroenabled.12

.potx application/vnd.openxmlformats-officedocument.presentationml.template

.ppa application/vnd.ms-powerpoint

.ppam application/vnd.ms-powerpoint.addin.macroenabled.12

.ppd application/vnd.cups-ppd

.ppm image/x-portable-pixmap

.pps application/vnd.ms-powerpoint

.ppsm application/vnd.ms-powerpoint.slideshow\.macroenabled.12

.ppsx application/vnd.openxmlformats-officedocument.presentationml.slideshow

.ppt application/vnd.ms-powerpoint

.pptm application/vnd.ms-powerpoint.presentation.macroenabled.12

.pptx application/vnd.openxmlformats-officedocument.presentationml.presentation

.pqa application/vnd.palm

.prc application/x-mobipocket-ebook

.pre application/vnd.lotus-freelance

.prf application/pics-rules

.prm application/octet-stream

.prx application/octet-stream

.ps application/postscript

.psb application/vnd.3gpp.pic-bw-small

.psc1 application/PowerShell

.psd image/vnd.adobe.photoshop

.psess application/xml

.psf application/x-font-linux-psf

.pskcxml application/pskc+xml

.psm application/octet-stream

.psp application/octet-stream

.ptid application/vnd.pvi.ptid1

.pub application/x-mspublisher

.pvb application/vnd.3gpp.pic-bw-var

.pwn application/vnd.3m.post-it-notes

.pwz application/vnd.ms-powerpoint

.pya audio/vnd.ms-playready.media.pya

.pyv video/vnd.ms-playready.media.pyv

.qam application/vnd.epson.quickanime

.qbo application/vnd.intu.qbo

.qfx application/vnd.intu.qfx

.qht text/x-html-insertion

.qhtm text/x-html-insertion

.qps application/vnd.publishare-delta-tree

.qt video/quicktime

.qti image/x-quicktime

.qtif image/x-quicktime

.qtl application/x-quicktimeplayer

.qwd application/vnd.quark.quarkxpress

.qwt application/vnd.quark.quarkxpress

.qxb application/vnd.quark.quarkxpress

.qxd application/vnd.quark.quarkxpress

.qxl application/vnd.quark.quarkxpress

.qxt application/vnd.quark.quarkxpress

.ra audio/x-pn-realaudio

.ram audio/x-pn-realaudio

.rar application/x-rar-compressed

.ras image/x-cmu-raster

.rat application/rat-file

.rc text/plain

.rc2 text/plain

.rcprofile application/vnd.ipunplugged.rcprofile

.rct text/plain

.rdf application/rdf+xml

.rdlc application/xml

.rdz application/vnd.data-vision.rdz

.rep application/vnd.businessobjects

.res application/x-dtbresource+xml

.resx application/xml

.rf image/vnd.rn-realflash

.rgb image/x-rgb

.rgs text/plain

.rif application/reginfo+xml

.rip audio/vnd.rip

.ris application/x-research-info-systems

.rl application/resource-lists+xml

.rlc image/vnd.fujixerox.edmics-rlc

.rld application/resource-lists-diff+xml

.rm application/vnd.rn-realmedia

.rmi audio/midi

.rmp audio/x-pn-realaudio-plugin

.rms application/vnd.jcp.javame.midlet-rms

.rmvb application/vnd.rn-realmedia-vbr

.rnc application/relax-ng-compact-syntax

.roa application/rpki-roa

.roff text/troff

.rp9 application/vnd.cloanto.rp9

.rpm audio/x-pn-realaudio-plugin

.rpss application/vnd.nokia.radio-presets

.rpst application/vnd.nokia.radio-preset

.rq application/sparql-query

.rqy text/x-ms-rqy

.rs application/rls-services+xml

.rsd application/rsd+xml

.rss application/rss+xml

.rtf application/rtf

.rtx text/richtext

.ruleset application/xml

.s text/x-asm

.s3m audio/s3m

.saf application/vnd.yamaha.smaf-audio

.safariextz application/x-safari-safariextz

.sbml application/sbml+xml

.sc application/vnd.ibm.secure-container

.scd application/x-msschedule

.scm application/vnd.lotus-screencam

.scq application/scvp-cv-request

.scs application/scvp-cv-response

.sct text/scriptlet

.scurl text/vnd.curl.scurl

.sd2 audio/x-sd2

.sda application/vnd.stardivision.draw

.sdc application/vnd.stardivision.calc

.sdd application/vnd.stardivision.impress

.sdkd application/vnd.solent.sdkm+xml

.sdkm application/vnd.solent.sdkm+xml

.sdp application/sdp

.sdw application/vnd.stardivision.writer

.sea application/octet-stream

.searchConnector-ms application/windows-search-connector+xml

.see application/vnd.seemail

.seed application/vnd.fdsn.seed

.sema application/vnd.sema

.semd application/vnd.semd

.semf application/vnd.semf

.ser application/java-serialized-object

.setpay application/set-payment-initiation

.setreg application/set-registration-initiation

.settings application/xml

.sfd-hdstx application/vnd.hydrostatix.sof-data

.sfs application/vnd.spotfire.sfs

.sfv text/x-sfv

.sgi image/sgi

.sgimb application/x-sgimb

.sgl application/vnd.stardivision.writer-global

.sgm text/sgml

.sgml text/sgml

.sh application/x-sh

.shar application/x-shar

.shf application/shf+xml

.shtml text/html

.sid image/x-mrsid-image

.sig application/pgp-signature

.sil audio/silk

.silo model/mesh

.sis application/vnd.symbian.install

.sisx application/vnd.symbian.install

.sit application/x-stuffit

.sitemap application/xml

.sitx application/x-stuffitx

.skd application/vnd.koan

.skin application/xml

.skm application/vnd.koan

.skp application/vnd.koan

.skt application/vnd.koan

.sldm application/vnd.ms-powerpoint.slide.macroenabled.12

.sldx application/vnd.openxmlformats-officedocument.presentationml.slide

.slk application/vnd.ms-excel

.sln text/plain

.slt application/vnd.epson.salt

.slupkg-ms application/x-ms-license

.sm application/vnd.stepmania.stepchart

.smd audio/x-smd

.smf application/vnd.stardivision.math

.smi application/smil+xml

.smil application/smil+xml

.smv video/x-smv

.smx audio/x-smd

.smz audio/x-smd

.smzip application/vnd.stepmania.package

.snd audio/basic

.snf application/x-font-snf

.snippet application/xml

.snp application/octet-stream

.so application/octet-stream

.sol text/plain

.sor text/plain

.spc application/x-pkcs7-certificates

.spf application/vnd.yamaha.smaf-phrase

.spl application/x-futuresplash

.spot text/vnd.in3d.spot

.spp application/scvp-vp-response

.spq application/scvp-vp-request

.spx audio/ogg

.sql application/x-sql

.src application/x-wais-source

.srf text/plain

.srt application/x-subrip

.sru application/sru+xml

.srx application/sparql-results+xml

.ssdl application/ssdl+xml

.sse application/vnd.kodak-descriptor

.ssf application/vnd.epson.ssf

.SSISDeploymentManifest text/xml

.ssm application/streamingmedia

.ssml application/ssml+xml

.sst application/vnd.ms-pki.certstore

.st application/vnd.sailingtracker.track

.stc application/vnd.sun.xml.calc.template

.std application/vnd.sun.xml.draw\.template

.stf application/vnd.wt.stf

.sti application/vnd.sun.xml.impress.template

.stk application/hyperstudio

.stl application/vnd.ms-pki.stl

.str application/vnd.pg.format

.stw application/vnd.sun.xml.writer.template

.sub text/vnd.dvb.subtitle

.sus application/vnd.sus-calendar

.susp application/vnd.sus-calendar

.sv4cpio application/x-sv4cpio

.sv4crc application/x-sv4crc

.svc application/vnd.dvb.service

.svd application/vnd.svd

.svg image/svg+xml

.svgz image/svg+xml

.swa application/x-director

.swf application/x-shockwave-flash

.swi application/vnd.aristanetworks.swi

.sxc application/vnd.sun.xml.calc

.sxd application/vnd.sun.xml.draw

.sxg application/vnd.sun.xml.writer.global

.sxi application/vnd.sun.xml.impress

.sxm application/vnd.sun.xml.math

.sxw application/vnd.sun.xml.writer

.t text/troff

.t3 application/x-t3vm-image

.taglet application/vnd.mynfc

.tao application/vnd.tao.intent-module-archive

.tar application/x-tar

.tcap application/vnd.3gpp2.tcap

.tcl application/x-tcl

.teacher application/vnd.smart.teacher

.tei application/tei+xml

.teicorpus application/tei+xml

.testrunconfig application/xml

.testsettings application/xml

.tex application/x-tex

.texi application/x-texinfo

.texinfo application/x-texinfo

.text text/plain

.tfi application/thraud+xml

.tfm application/x-tex-tfm

.tga image/x-tga

.tgz application/x-compressed

.thmx application/vnd.ms-officetheme

.thn application/octet-stream

.tif image/tiff

.tiff image/tiff

.tlh text/plain

.tli text/plain

.tmo application/vnd.tmobile-livetv

.toc application/octet-stream

.torrent application/x-bittorrent

.tpl application/vnd.groove-tool-template

.tpt application/vnd.trid.tpt

.tr text/troff

.tra application/vnd.trueapp

.trm application/x-msterminal

.trx application/xml

.ts video/vnd.dlna.mpeg-tts

.tsd application/timestamped-data

.tsv text/tab-separated-values

.ttc application/x-font-ttf

.ttf application/x-font-ttf

.ttl text/turtle

.tts video/vnd.dlna.mpeg-tts

.twd application/vnd.simtech-mindmapper

.twds application/vnd.simtech-mindmapper

.txd application/vnd.genomatix.tuxedo

.txf application/vnd.mobius.txf

.txt text/plain

.u32 application/x-authorware-bin

.udeb application/x-debian-package

.ufd application/vnd.ufdl

.ufdl application/vnd.ufdl

.uls text/iuls

.ulx application/x-glulx

.umj application/vnd.umajin

.unityweb application/vnd.unity

.uoml application/vnd.uoml+xml

.uri text/uri-list

.uris text/uri-list

.urls text/uri-list

.user text/plain

.ustar application/x-ustar

.utz application/vnd.uiq.theme

.uu text/x-uuencode

.uva audio/vnd.dece.audio

.uvd application/vnd.dece.data

.uvf application/vnd.dece.data

.uvg image/vnd.dece.graphic

.uvh video/vnd.dece.hd

.uvi image/vnd.dece.graphic

.uvm video/vnd.dece.mobile

.uvp video/vnd.dece.pd

.uvs video/vnd.dece.sd

.uvt application/vnd.dece.ttml+xml

.uvu video/vnd.uvvu.mp4

.uvv video/vnd.dece.video

.uvva audio/vnd.dece.audio

.uvvd application/vnd.dece.data

.uvvf application/vnd.dece.data

.uvvg image/vnd.dece.graphic

.uvvh video/vnd.dece.hd

.uvvi image/vnd.dece.graphic

.uvvm video/vnd.dece.mobile

.uvvp video/vnd.dece.pd

.uvvs video/vnd.dece.sd

.uvvt application/vnd.dece.ttml+xml

.uvvu video/vnd.uvvu.mp4

.uvvv video/vnd.dece.video

.uvvx application/vnd.dece.unspecified

.uvvz application/vnd.dece.zip

.uvx application/vnd.dece.unspecified

.uvz application/vnd.dece.zip

.vb text/plain

.vbdproj text/plain

.vbk video/mpeg

.vbproj text/plain

.vbs text/vbscript

.vcard text/vcard

.vcd application/x-cdlink

.vcf text/x-vcard

.vcg application/vnd.groove-vcard

.vcproj Application/xml

.vcs text/x-vcalendar

.vcx application/vnd.vcx

.vcxproj Application/xml

.vddproj text/plain

.vdp text/plain

.vdproj text/plain

.vdx application/vnd.ms-visio.viewer

.vis application/vnd.visionary

.viv video/vnd.vivo

.vml text/xml

.vob video/x-ms-vob

.vor application/vnd.stardivision.writer

.vox application/x-authorware-bin

.vrml model/vrml

.vscontent application/xml

.vsct text/xml

.vsd application/vnd.visio

.vsf application/vnd.vsf

.vsi application/ms-vsi

.vsix application/vsix

.vsixlangpack text/xml

.vsixmanifest text/xml

.vsmdi application/xml

.vspscc text/plain

.vss application/vnd.visio

.vsscc text/plain

.vssettings text/xml

.vssscc text/plain

.vst application/vnd.visio

.vstemplate text/xml

.vsto application/x-ms-vsto

.vsw application/vnd.visio

.vsx application/vnd.visio

.vtu model/vnd.vtu

.vtx application/vnd.visio

.vxml application/voicexml+xml

.w3d application/x-director

.wad application/x-doom

.wav audio/x-wav

.wave audio/wav

.wax audio/x-ms-wax

.wbk application/msword

.wbmp image/vnd.wap.wbmp

.wbs application/vnd.criticaltools.wbs+xml

.wbxml application/vnd.wap.wbxml

.wcm application/vnd.ms-works

.wdb application/vnd.ms-works

.wdp image/vnd.ms-photo

.weba audio/webm

.webarchive application/x-safari-webarchive

.webm video/webm

.webp image/webp

.webtest application/xml

.wg application/vnd.pmi.widget

.wgt application/widget

.wiq application/xml

.wiz application/msword

.wks application/vnd.ms-works

.WLMP application/wlmoviemaker

.wlpginstall application/x-wlpg-detect

.wlpginstall3 application/x-wlpg3-detect

.wm video/x-ms-wm

.wma audio/x-ms-wma

.wmd application/x-ms-wmd

.wmf application/x-msmetafile

.wml text/vnd.wap.wml

.wmlc application/vnd.wap.wmlc

.wmls text/vnd.wap.wmlscript

.wmlsc application/vnd.wap.wmlscriptc

.wmp video/x-ms-wmp

.wmv video/x-ms-wmv

.wmx video/x-ms-wmx

.wmz application/x-ms-wmz

.woff application/font-woff

.wpd application/vnd.wordperfect

.wpl application/vnd.ms-wpl

.wps application/vnd.ms-works

.wqd application/vnd.wqd

.wri application/x-mswrite

.wrl model/vrml

.wrz x-world/x-vrml

.wsc text/scriptlet

.wsdl application/wsdl+xml

.wspolicy application/wspolicy+xml

.wtb application/vnd.webturbo

.wvx video/x-ms-wvx

.x application/directx

.x32 application/x-authorware-bin

.x3d model/x3d+xml

.x3db model/x3d+binary

.x3dbz model/x3d+binary

.x3dv model/x3d+vrml

.x3dvz model/x3d+vrml

.x3dz model/x3d+xml

.xaf x-world/x-vrml

.xaml application/xaml+xml

.xap application/x-silverlight-app

.xar application/vnd.xara

.xbap application/x-ms-xbap

.xbd application/vnd.fujixerox.docuworks.binder

.xbm image/x-xbitmap

.xdf application/xcap-diff+xml

.xdm application/vnd.syncml.dm+xml

.xdp application/vnd.adobe.xdp+xml

.xdr text/plain

.xdssc application/dssc+xml

.xdw application/vnd.fujixerox.docuworks

.xenc application/xenc+xml

.xer application/patch-ops-error+xml

.xfdf application/vnd.adobe.xfdf

.xfdl application/vnd.xfdl

.xht application/xhtml+xml

.xhtml application/xhtml+xml

.xhvml application/xv+xml

.xif image/vnd.xiff

.xla application/vnd.ms-excel

.xlam application/vnd.ms-excel.addin.macroenabled.12

.xlc application/vnd.ms-excel

.xld application/vnd.ms-excel

.xlf application/x-xliff+xml

.xlk application/vnd.ms-excel

.xll application/vnd.ms-excel

.xlm application/vnd.ms-excel

.xls application/vnd.ms-excel

.xlsb application/vnd.ms-excel.sheet.binary.macroenabled.12

.xlsm application/vnd.ms-excel.sheet.macroenabled.12

.xlsx application/vnd.openxmlformats-officedocument.spreadsheetml.sheet

.xlt application/vnd.ms-excel

.xltm application/vnd.ms-excel.template.macroenabled.12

.xltx application/vnd.openxmlformats-officedocument.spreadsheetml.template

.xlw application/vnd.ms-excel

.xm audio/xm

.xml application/xml

.xmta application/xml

.xo application/vnd.olpc-sugar

.xof x-world/x-vrml

.XOML text/plain

.xop application/xop+xml

.xpi application/x-xpinstall

.xpl application/xproc+xml

.xpm image/x-xpixmap

.xpr application/vnd.is-xpr

.xps application/vnd.ms-xpsdocument

.xpw application/vnd.intercon.formnet

.xpx application/vnd.intercon.formnet

.xrm-ms text/xml

.xsc application/xml

.xsd text/xml

.xsf text/xml

.xsl application/xml

.xslt application/xslt+xml

.xsm application/vnd.syncml+xml

.xsn application/octet-stream

.xspf application/xspf+xml

.xss application/xml

.xtp application/octet-stream

.xul application/vnd.mozilla.xul+xml

.xvm application/xv+xml

.xvml application/xv+xml

.xwd image/x-xwindowdump

.xyz chemical/x-xyz

.xz application/x-xz

.yang application/yang

.yin application/yin+xml

.z application/x-compress

.z1 application/x-zmachine

.z2 application/x-zmachine

.z3 application/x-zmachine

.z4 application/x-zmachine

.z5 application/x-zmachine

.z6 application/x-zmachine

.z7 application/x-zmachine

.z8 application/x-zmachine

.zaz application/vnd.zzazz.deck+xml

.zip application/zip

.zir application/vnd.zul

.zirz application/vnd.zul

.zmm application/vnd.handheld-entertainment+xml



\# From https\://github.com/Kentico/Mvc/blob/master/src/DancingGoat/App_Data/mimetypes.txt

ai application/postscript

aif audio/x-aiff

aifc audio/x-aiff

aiff audio/x-aiff

asc application/pgp

asf video/x-ms-asf

asx video/x-ms-asf

au audio/basic

avi video/x-msvideo

bcpio application/x-bcpio

bin application/octet-stream

bmp image/bmp

c text/plain

c++ text/plain

cc text/plain

cs text/plain

cpp text/x-c++src

cxx text/x-c++src

cdf application/x-netcdf

class application/octet-stream

com application/octet-stream

cpio application/x-cpio

cpt application/mac-compactpro

csh application/x-csh

css text/css

csv text/comma-separated-values

dcr application/x-director

diff text/diff

dir application/x-director

dll application/octet-stream

dms application/octet-stream

doc application/msword

docx application/vnd.openxmlformats-officedocument.wordprocessingml.document

dotx application/vnd.openxmlformats-officedocument.wordprocessingml.template

docm application/vnd.ms-word.document.macroEnabled.12

dotm application/vnd.ms-word.template.macroEnabled.12

dot application/msword

dvi application/x-dvi

dxr application/x-director

eps application/postscript

etx text/x-setext

exe application/octet-stream

ez application/andrew-inset

flv video/x-flv

gif image/gif

gtar application/x-gtar

gz application/x-gzip

h text/plain

h++ text/plain

hh text/plain

hpp text/plain

hxx text/plain

hdf application/x-hdf

hqx application/mac-binhex40

htm text/html

html text/html

ice x-conference/x-cooltalk

ics text/calendar

ief image/ief

ifb text/calendar

iges model/iges

igs model/iges

jar application/x-jar

java text/x-java-source

jpe image/jpeg

jpeg image/jpeg

jpg image/jpeg

js application/x-javascript

kar audio/midi

latex application/x-latex

lha application/octet-stream

log text/plain

lzh application/octet-stream

m3u audio/x-mpegurl

man application/x-troff-man

me application/x-troff-me

mesh model/mesh

mid audio/midi

midi audio/midi

mif application/vnd.mif

mov video/quicktime

movie video/x-sgi-movie

mp2 audio/mpeg

mp3 audio/mpeg

mpe video/mpeg

mpeg video/mpeg

mpg video/mpeg

mpga audio/mpeg

ms application/x-troff-ms

msh model/mesh

mxu video/vnd.mpegurl

nc application/x-netcdf

oda application/oda

patch text/diff

pbm image/x-portable-bitmap

pdb chemical/x-pdb

pdf application/pdf

pgm image/x-portable-graymap

pgn application/x-chess-pgn

pgp application/pgp

php application/x-httpd-php

php3 application/x-httpd-php3

pl application/x-perl

pm application/x-perl

png image/png

pnm image/x-portable-anymap

po text/plain

ps application/postscript

potm application/vnd.ms-powerpoint.template.macroEnabled.12

potx application/vnd.openxmlformats-officedocument.presentationml.template

ppm image/x-portable-pixmap

ppsx application/vnd.openxmlformats-officedocument.presentationml.slideshow

ppam application/vnd.ms-powerpoint.addin.macroEnabled.12

ppsm application/vnd.ms-powerpoint.slideshow\.macroEnabled.12

ppt application/vnd.ms-powerpoint

pptm application/vnd.ms-powerpoint.presentation.macroEnabled.12

pptx application/vnd.openxmlformats-officedocument.presentationml.presentation

qt video/quicktime

ra audio/x-realaudio

ram audio/x-pn-realaudio

ras image/x-cmu-raster

rgb image/x-rgb

rm audio/x-pn-realaudio

roff application/x-troff

rpm audio/x-pn-realaudio-plugin

rtf text/rtf

rtx text/richtext

sgm text/sgml

sgml text/sgml

sh application/x-sh

shar application/x-shar

shtml text/html

silo model/mesh

sit application/x-stuffit

skd application/x-koan

skm application/x-koan

skp application/x-koan

skt application/x-koan

smi application/smil

smil application/smil

snd audio/basic

so application/octet-stream

spl application/x-futuresplash

src application/x-wais-source

stc application/vnd.sun.xml.calc.template

std application/vnd.sun.xml.draw\.template

sti application/vnd.sun.xml.impress.template

stw application/vnd.sun.xml.writer.template

sv4cpio application/x-sv4cpio

sv4crc application/x-sv4crc

swf application/x-shockwave-flash

sxc application/vnd.sun.xml.calc

sxd application/vnd.sun.xml.draw

sxg application/vnd.sun.xml.writer.global

sxi application/vnd.sun.xml.impress

sxm application/vnd.sun.xml.math

sxw application/vnd.sun.xml.writer

t application/x-troff

tar application/x-tar

tcl application/x-tcl

tex application/x-tex

texi application/x-texinfo

texinfo application/x-texinfo

tgz application/x-gtar

tif image/tiff

tiff image/tiff

tr application/x-troff

tsv text/tab-separated-values

txt text/plain

ustar application/x-ustar

vbs text/plain

vcd application/x-cdlink

vcf text/x-vcard

vcs text/calendar

vfb text/calendar

vrml model/vrml

vsd application/vnd.visio

wav audio/x-wav

wax audio/x-ms-wax

wbmp image/vnd.wap.wbmp

wbxml application/vnd.wap.wbxml

wm video/x-ms-wm

wma audio/x-ms-wma

wmd application/x-ms-wmd

wml text/vnd.wap.wml

wmlc application/vnd.wap.wmlc

wmls text/vnd.wap.wmlscript

wmlsc application/vnd.wap.wmlscriptc

wmv video/x-ms-wmv

wmx video/x-ms-wmx

wmz application/x-ms-wmz

wrl model/vrml

wvx video/x-ms-wvx

xbm image/x-xbitmap

xht application/xhtml+xml

xhtml application/xhtml+xml

xlam application/vnd.ms-excel.addin.macroEnabled.12

xls application/vnd.ms-excel

xlsb application/vnd.ms-excel.sheet.binary.macroEnabled.12

xlsx application/vnd.openxmlformats-officedocument.spreadsheetml.sheet

xlt application/vnd.ms-excel

xltm application/vnd.ms-excel.template.macroEnabled.12

xltx application/vnd.openxmlformats-officedocument.spreadsheetml.template

xml application/xml

xpm image/x-xpixmap

xsl text/xml

xwd image/x-xwindowdump

xyz chemical/x-xyz

z application/x-compress

zip application/zip

mp4 video/mp4

webm video/webm

ogg video/ogg

oga audio/ogg

ogv video/ogg

woff application/font-woff



\# From https\://github.com/samuelneff/MimeTypeMap/blob/062f83f528b8e6c15176b1feb888132cb0f52948/src/MimeTypes/MimeTypeMap.cs

.323 text/h323

.3g2 video/3gpp2

.3gp video/3gpp

.3gp2 video/3gpp2

.3gpp video/3gpp

.7z application/x-7z-compressed

.aa audio/audible

.AAC audio/aac

.aaf application/octet-stream

.aax audio/vnd.audible.aax

.ac3 audio/ac3

.aca application/octet-stream

.accda application/msaccess.addin

.accdb application/msaccess

.accdc application/msaccess.cab

.accde application/msaccess

.accdr application/msaccess.runtime

.accdt application/msaccess

.accdw application/msaccess.webapplication

.accft application/msaccess.ftemplate

.acx application/internet-property-stream

.AddIn text/xml

.ade application/msaccess

.adobebridge application/x-bridge-url

.adp application/msaccess

.ADT audio/vnd.dlna.adts

.ADTS audio/aac

.afm application/octet-stream

.ai application/postscript

.aif audio/aiff

.aifc audio/aiff

.aiff audio/aiff

.air application/vnd.adobe.air-application-installer-package+zip

.amc application/mpeg

.anx application/annodex

.apk application/vnd.android.package-archive

.application application/x-ms-application

.art image/x-jg

.asa application/xml

.asax application/xml

.ascx application/xml

.asd application/octet-stream

.asf video/x-ms-asf

.ashx application/xml

.asi application/octet-stream

.asm text/plain

.asmx application/xml

.aspx application/xml

.asr video/x-ms-asf

.asx video/x-ms-asf

.atom application/atom+xml

.au audio/basic

.avi video/x-msvideo

.axa audio/annodex

.axs application/olescript

.axv video/annodex

.bas text/plain

.bcpio application/x-bcpio

.bin application/octet-stream

.bmp image/bmp

.c text/plain

.cab application/octet-stream

.caf audio/x-caf

.calx application/vnd.ms-office.calx

.cat application/vnd.ms-pki.seccat

.cc text/plain

.cd text/plain

.cdda audio/aiff

.cdf application/x-cdf

.cer application/x-x509-ca-cert

.cfg text/plain

.chm application/octet-stream

.class application/x-java-applet

.clp application/x-msclip

.cmd text/plain

.cmx image/x-cmx

.cnf text/plain

.cod image/cis-cod

.config application/xml

.contact text/x-ms-contact

.coverage application/xml

.cpio application/x-cpio

.cpp text/plain

.crd application/x-mscardfile

.crl application/pkix-crl

.crt application/x-x509-ca-cert

.cs text/plain

.csdproj text/plain

.csh application/x-csh

.csproj text/plain

.css text/css

.csv text/csv

.cur application/octet-stream

.cxx text/plain

.dat application/octet-stream

.datasource application/xml

.dbproj text/plain

.dcr application/x-director

.def text/plain

.deploy application/octet-stream

.der application/x-x509-ca-cert

.dgml application/xml

.dib image/bmp

.dif video/x-dv

.dir application/x-director

.disco text/xml

.divx video/divx

.dll application/x-msdownload

.dll.config text/xml

.dlm text/dlm

.doc application/msword

.docm application/vnd.ms-word.document.macroEnabled.12

.docx application/vnd.openxmlformats-officedocument.wordprocessingml.document

.dot application/msword

.dotm application/vnd.ms-word.template.macroEnabled.12

.dotx application/vnd.openxmlformats-officedocument.wordprocessingml.template

.dsp application/octet-stream

.dsw text/plain

.dtd text/xml

.dtsConfig text/xml

.dv video/x-dv

.dvi application/x-dvi

.dwf drawing/x-dwf

.dwg application/acad

.dwp application/octet-stream

.dxf application/x-dxf

.dxr application/x-director

.eml message/rfc822

.emz application/octet-stream

.eot application/vnd.ms-fontobject

.eps application/postscript

.es application/ecmascript

.etl application/etl

.etx text/x-setext

.evy application/envoy

.exe application/octet-stream

.exe.config text/xml

.fdf application/vnd.fdf

.fif application/fractals

.filters application/xml

.fla application/octet-stream

.flac audio/flac

.flr x-world/x-vrml

.flv video/x-flv

.fsscript application/fsharp-script

.fsx application/fsharp-script

.generictest application/xml

.gif image/gif

.gpx application/gpx+xml

.group text/x-ms-group

.gsm audio/x-gsm

.gtar application/x-gtar

.gz application/x-gzip

.h text/plain

.hdf application/x-hdf

.hdml text/x-hdml

.hhc application/x-oleobject

.hhk application/octet-stream

.hhp application/octet-stream

.hlp application/winhlp

.hpp text/plain

.hqx application/mac-binhex40

.hta application/hta

.htc text/x-component

.htm text/html

.html text/html

.htt text/webviewhtml

.hxa application/xml

.hxc application/xml

.hxd application/octet-stream

.hxe application/xml

.hxf application/xml

.hxh application/octet-stream

.hxi application/octet-stream

.hxk application/xml

.hxq application/octet-stream

.hxr application/octet-stream

.hxs application/octet-stream

.hxt text/html

.hxv application/xml

.hxw application/octet-stream

.hxx text/plain

.i text/plain

.ico image/x-icon

.ics application/octet-stream

.idl text/plain

.ief image/ief

.iii application/x-iphone

.inc text/plain

.inf application/octet-stream

.ini text/plain

.inl text/plain

.ins application/x-internet-signup

.ipa application/x-itunes-ipa

.ipg application/x-itunes-ipg

.ipproj text/plain

.ipsw application/x-itunes-ipsw

.iqy text/x-ms-iqy

.isp application/x-internet-signup

.ite application/x-itunes-ite

.itlp application/x-itunes-itlp

.itms application/x-itunes-itms

.itpc application/x-itunes-itpc

.IVF video/x-ivf

.jar application/java-archive

.java application/octet-stream

.jck application/liquidmotion

.jcz application/liquidmotion

.jfif image/pjpeg

.jnlp application/x-java-jnlp-file

.jpb application/octet-stream

.jpe image/jpeg

.jpeg image/jpeg

.jpg image/jpeg

.js application/javascript

.json application/json

.jsx text/jscript

.jsxbin text/plain

.latex application/x-latex

.library-ms application/windows-library+xml

.lit application/x-ms-reader

.loadtest application/xml

.lpk application/octet-stream

.lsf video/x-la-asf

.lst text/plain

.lsx video/x-la-asf

.lzh application/octet-stream

.m13 application/x-msmediaview

.m14 application/x-msmediaview

.m1v video/mpeg

.m2t video/vnd.dlna.mpeg-tts

.m2ts video/vnd.dlna.mpeg-tts

.m2v video/mpeg

.m3u audio/x-mpegurl

.m3u8 audio/x-mpegurl

.m4a audio/m4a

.m4b audio/m4b

.m4p audio/m4p

.m4r audio/x-m4r

.m4v video/x-m4v

.mac image/x-macpaint

.mak text/plain

.man application/x-troff-man

.manifest application/x-ms-manifest

.map text/plain

.master application/xml

.mbox application/mbox

.mda application/msaccess

.mdb application/x-msaccess

.mde application/msaccess

.mdp application/octet-stream

.me application/x-troff-me

.mfp application/x-shockwave-flash

.mht message/rfc822

.mhtml message/rfc822

.mid audio/mid

.midi audio/mid

.mix application/octet-stream

.mk text/plain

.mk3d video/x-matroska-3d

.mka audio/x-matroska

.mkv video/x-matroska

.mmf application/x-smaf

.mno text/xml

.mny application/x-msmoney

.mod video/mpeg

.mov video/quicktime

.movie video/x-sgi-movie

.mp2 video/mpeg

.mp2v video/mpeg

.mp3 audio/mpeg

.mp4 video/mp4

.mp4v video/mp4

.mpa video/mpeg

.mpe video/mpeg

.mpeg video/mpeg

.mpf application/vnd.ms-mediapackage

.mpg video/mpeg

.mpp application/vnd.ms-project

.mpv2 video/mpeg

.mqv video/quicktime

.ms application/x-troff-ms

.msg application/vnd.ms-outlook

.msi application/octet-stream

.mso application/octet-stream

.mts video/vnd.dlna.mpeg-tts

.mtx application/xml

.mvb application/x-msmediaview

.mvc application/x-miva-compiled

.mxp application/x-mmxp

.nc application/x-netcdf

.nsc video/x-ms-asf

.nws message/rfc822

.ocx application/octet-stream

.oda application/oda

.odb application/vnd.oasis.opendocument.database

.odc application/vnd.oasis.opendocument.chart

.odf application/vnd.oasis.opendocument.formula

.odg application/vnd.oasis.opendocument.graphics

.odh text/plain

.odi application/vnd.oasis.opendocument.image

.odl text/plain

.odm application/vnd.oasis.opendocument.text-master

.odp application/vnd.oasis.opendocument.presentation

.ods application/vnd.oasis.opendocument.spreadsheet

.odt application/vnd.oasis.opendocument.text

.oga audio/ogg

.ogg audio/ogg

.ogv video/ogg

.ogx application/ogg

.one application/onenote

.onea application/onenote

.onepkg application/onenote

.onetmp application/onenote

.onetoc application/onenote

.onetoc2 application/onenote

.opus audio/ogg

.orderedtest application/xml

.osdx application/opensearchdescription+xml

.otf application/font-sfnt

.otg application/vnd.oasis.opendocument.graphics-template

.oth application/vnd.oasis.opendocument.text-web

.otp application/vnd.oasis.opendocument.presentation-template

.ots application/vnd.oasis.opendocument.spreadsheet-template

.ott application/vnd.oasis.opendocument.text-template

.oxt application/vnd.openofficeorg.extension

.p10 application/pkcs10

.p12 application/x-pkcs12

.p7b application/x-pkcs7-certificates

.p7c application/pkcs7-mime

.p7m application/pkcs7-mime

.p7r application/x-pkcs7-certreqresp

.p7s application/pkcs7-signature

.pbm image/x-portable-bitmap

.pcast application/x-podcast

.pct image/pict

.pcx application/octet-stream

.pcz application/octet-stream

.pdf application/pdf

.pfb application/octet-stream

.pfm application/octet-stream

.pfx application/x-pkcs12

.pgm image/x-portable-graymap

.pic image/pict

.pict image/pict

.pkgdef text/plain

.pkgundef text/plain

.pko application/vnd.ms-pki.pko

.pls audio/scpls

.pma application/x-perfmon

.pmc application/x-perfmon

.pml application/x-perfmon

.pmr application/x-perfmon

.pmw application/x-perfmon

.png image/png

.pnm image/x-portable-anymap

.pnt image/x-macpaint

.pntg image/x-macpaint

.pnz image/png

.pot application/vnd.ms-powerpoint

.potm application/vnd.ms-powerpoint.template.macroEnabled.12

.potx application/vnd.openxmlformats-officedocument.presentationml.template

.ppa application/vnd.ms-powerpoint

.ppam application/vnd.ms-powerpoint.addin.macroEnabled.12

.ppm image/x-portable-pixmap

.pps application/vnd.ms-powerpoint

.ppsm application/vnd.ms-powerpoint.slideshow\.macroEnabled.12

.ppsx application/vnd.openxmlformats-officedocument.presentationml.slideshow

.ppt application/vnd.ms-powerpoint

.pptm application/vnd.ms-powerpoint.presentation.macroEnabled.12

.pptx application/vnd.openxmlformats-officedocument.presentationml.presentation

.prf application/pics-rules

.prm application/octet-stream

.prx application/octet-stream

.ps application/postscript

.psc1 application/PowerShell

.psd application/octet-stream

.psess application/xml

.psm application/octet-stream

.psp application/octet-stream

.pst application/vnd.ms-outlook

.pub application/x-mspublisher

.pwz application/vnd.ms-powerpoint

.qht text/x-html-insertion

.qhtm text/x-html-insertion

.qt video/quicktime

.qti image/x-quicktime

.qtif image/x-quicktime

.qtl application/x-quicktimeplayer

.qxd application/octet-stream

.ra audio/x-pn-realaudio

.ram audio/x-pn-realaudio

.rar application/x-rar-compressed

.ras image/x-cmu-raster

.rat application/rat-file

.rc text/plain

.rc2 text/plain

.rct text/plain

.rdlc application/xml

.reg application/octet-stream

.resx application/xml

.rf image/vnd.rn-realflash

.rgb image/x-rgb

.rgs text/plain

.rm application/vnd.rn-realmedia

.rmi audio/mid

.rmp application/vnd.rn-rn_music_package

.roff application/x-troff

.rpm audio/x-pn-realaudio-plugin

.rqy text/x-ms-rqy

.rtf application/rtf

.rtx text/richtext

.rvt application/octet-stream

.ruleset application/xml

.s text/plain

.safariextz application/x-safari-safariextz

.scd application/x-msschedule

.scr text/plain

.sct text/scriptlet

.sd2 audio/x-sd2

.sdp application/sdp

.sea application/octet-stream

.searchConnector-ms application/windows-search-connector+xml

.setpay application/set-payment-initiation

.setreg application/set-registration-initiation

.settings application/xml

.sgimb application/x-sgimb

.sgml text/sgml

.sh application/x-sh

.shar application/x-shar

.shtml text/html

.sit application/x-stuffit

.sitemap application/xml

.skin application/xml

.skp application/x-koan

.sldm application/vnd.ms-powerpoint.slide.macroEnabled.12

.sldx application/vnd.openxmlformats-officedocument.presentationml.slide

.slk application/vnd.ms-excel

.sln text/plain

.slupkg-ms application/x-ms-license

.smd audio/x-smd

.smi application/octet-stream

.smx audio/x-smd

.smz audio/x-smd

.snd audio/basic

.snippet application/xml

.snp application/octet-stream

.sol text/plain

.sor text/plain

.spc application/x-pkcs7-certificates

.spl application/futuresplash

.spx audio/ogg

.src application/x-wais-source

.srf text/plain

.SSISDeploymentManifest text/xml

.ssm application/streamingmedia

.sst application/vnd.ms-pki.certstore

.stl application/vnd.ms-pki.stl

.sv4cpio application/x-sv4cpio

.sv4crc application/x-sv4crc

.svc application/xml

.svg image/svg+xml

.swf application/x-shockwave-flash

.step application/step

.stp application/step

.t application/x-troff

.tar application/x-tar

.tcl application/x-tcl

.testrunconfig application/xml

.testsettings application/xml

.tex application/x-tex

.texi application/x-texinfo

.texinfo application/x-texinfo

.tgz application/x-compressed

.thmx application/vnd.ms-officetheme

.thn application/octet-stream

.tif image/tiff

.tiff image/tiff

.tlh text/plain

.tli text/plain

.toc application/octet-stream

.tr application/x-troff

.trm application/x-msterminal

.trx application/xml

.ts video/vnd.dlna.mpeg-tts

.tsv text/tab-separated-values

.ttf application/font-sfnt

.tts video/vnd.dlna.mpeg-tts

.txt text/plain

.u32 application/octet-stream

.uls text/iuls

.user text/plain

.ustar application/x-ustar

.vb text/plain

.vbdproj text/plain

.vbk video/mpeg

.vbproj text/plain

.vbs text/vbscript

.vcf text/x-vcard

.vcproj application/xml

.vcs text/plain

.vcxproj application/xml

.vddproj text/plain

.vdp text/plain

.vdproj text/plain

.vdx application/vnd.ms-visio.viewer

.vml text/xml

.vscontent application/xml

.vsct text/xml

.vsd application/vnd.visio

.vsi application/ms-vsi

.vsix application/vsix

.vsixlangpack text/xml

.vsixmanifest text/xml

.vsmdi application/xml

.vspscc text/plain

.vss application/vnd.visio

.vsscc text/plain

.vssettings text/xml

.vssscc text/plain

.vst application/vnd.visio

.vstemplate text/xml

.vsto application/x-ms-vsto

.vsw application/vnd.visio

.vsx application/vnd.visio

.vtt text/vtt

.vtx application/vnd.visio

.wasm application/wasm

.wav audio/wav

.wave audio/wav

.wax audio/x-ms-wax

.wbk application/msword

.wbmp image/vnd.wap.wbmp

.wcm application/vnd.ms-works

.wdb application/vnd.ms-works

.wdp image/vnd.ms-photo

.webarchive application/x-safari-webarchive

.webm video/webm

.webp image/webp

.webtest application/xml

.wiq application/xml

.wiz application/msword

.wks application/vnd.ms-works

.WLMP application/wlmoviemaker

.wlpginstall application/x-wlpg-detect

.wlpginstall3 application/x-wlpg3-detect

.wm video/x-ms-wm

.wma audio/x-ms-wma

.wmd application/x-ms-wmd

.wmf application/x-msmetafile

.wml text/vnd.wap.wml

.wmlc application/vnd.wap.wmlc

.wmls text/vnd.wap.wmlscript

.wmlsc application/vnd.wap.wmlscriptc

.wmp video/x-ms-wmp

.wmv video/x-ms-wmv

.wmx video/x-ms-wmx

.wmz application/x-ms-wmz

.woff application/font-woff

.woff2 application/font-woff2

.wpl application/vnd.ms-wpl

.wps application/vnd.ms-works

.wri application/x-mswrite

.wrl x-world/x-vrml

.wrz x-world/x-vrml

.wsc text/scriptlet

.wsdl text/xml

.wvx video/x-ms-wvx

.x application/directx

.xaf x-world/x-vrml

.xaml application/xaml+xml

.xap application/x-silverlight-app

.xbap application/x-ms-xbap

.xbm image/x-xbitmap

.xdr text/plain

.xht application/xhtml+xml

.xhtml application/xhtml+xml

.xla application/vnd.ms-excel

.xlam application/vnd.ms-excel.addin.macroEnabled.12

.xlc application/vnd.ms-excel

.xld application/vnd.ms-excel

.xlk application/vnd.ms-excel

.xll application/vnd.ms-excel

.xlm application/vnd.ms-excel

.xls application/vnd.ms-excel

.xlsb application/vnd.ms-excel.sheet.binary.macroEnabled.12

.xlsm application/vnd.ms-excel.sheet.macroEnabled.12

.xlsx application/vnd.openxmlformats-officedocument.spreadsheetml.sheet

.xlt application/vnd.ms-excel

.xltm application/vnd.ms-excel.template.macroEnabled.12

.xltx application/vnd.openxmlformats-officedocument.spreadsheetml.template

.xlw application/vnd.ms-excel

.xml text/xml

.xmp application/octet-stream

.xmta application/xml

.xof x-world/x-vrml

.XOML text/plain

.xpm image/x-xpixmap

.xps application/vnd.ms-xpsdocument

.xrm-ms text/xml

.xsc application/xml

.xsd text/xml

.xsf text/xml

.xsl text/xml

.xslt text/xml

.xsn application/octet-stream

.xss application/xml

.xspf application/xspf+xml

.xtp application/octet-stream

.xwd image/x-xwindowdump

.z application/x-compress

.zip application/zip







\# 特別上書き

.sh text/plain;charset=UTF-8 #overwrite

.md text/plain;charset=UTF-8 #overwrite

.config text/plain;charset=UTF-8 #overwrite



\# OpenVPN

.ovpn application/x-openvpn-profile





\--- [T261018_EL_VUFEJ_33] ここまで ---





\--- [R1.specs] ここまで ---



【AI指示】調査・検索する際のサイトや情報源は、日本語に限らず、英語やその他の言語の情報源も広く用いてください。ただし、出力結果は、日本語で返してください。





\--- [CODE_RULE:CS_NETFX48_APP_VER2601008] ここから ---



1\. Microsoft Visual Studio 2026 でビルド可能な、Windows 用 Visual C# スタンドアロン WinExe (コンソールまたは GUI。いずれが適切であるかは、) アプリケーション (.NET Framework 4.8 を利用) (CPU は、できれば any。しかし、何らかの理由でビット固定する必要があれば、当該固定ビットを示した上で、それを利用可能) の c# ソースコードを作れ。使用可能な C# 言語バージョンは当該環境用のものとする。



できる限り標準ライブラリのみで動作する (nuget などで追加ライブラリを導入不要とする)、Windows 上で動作する C# (.cs) ソースコードを作成せよ。



2\. ソースコードファイル名について



(場合 1. 今回実行するタスクが、初版、すなわち VAR_FIRST_VERSION の場合)

ソースコードファイル名は、本プロンプトの標題名における「DNNT」および後続の 13 文字半角英数記号をすべて半角小文字にし、アンダーバーでつなげた「dnnt_xxxxxxxxxxxxx」形式の 18 文字に、さらにアンダーバーを 1 個付け、その後にスクリプトの目的を英語で意味する簡潔な 24 文字程度以内のlower caseのアンダーバー区切り (アルファベットおよび数字およびアンダーバーのみ使用可能) 英語文字列を付け、最後に、「r01」などの (VAR_FIRST_VERSION の R 番号をそのまま転記、ただし小文字) バージョン番号、'\_'、および YYMMDD (JST における本タスク実行年月日 6 桁) およびこのタスクの本プロンプトの標題名における「DNNT」の後の数字6桁+ハイフンの後に出現する "英数字大文字 6 桁" を小文字化したもの (次のサンプルでは "abcdef" とする) を追記したもの (「dnnt_xxxxxxxxxxxxx」 の xxxxxxxxxxxxx と abcdef は一部重複する可能性があるが問題無い) とし、拡張子は .cs とする。生成 .cs ファイルは、別途ユーザ指示がない限り、 1 個に限る --- [YGJ9E7ZV]。(ファイル名の例: dnnt_xxxxxxxxxxxxx_functiondesc_r01_yymmdd_abcdef.cs)



&#x20;   内部名 (プログラムの内部基本識別文字列や生成実行可能ファイル名に使われるような文字列) は、「dnnt_xxxxxxxxxxxxx_functiondesc」とし、「r01_yymmdd_abcdef」部分は不要。



(場合 2. 今回実行するタスクが、初版ではなく、前のソースコードの改良行為である場合)

ソースコードファイル名は、入力ソースコードのファイル名 (それはおおむね 「場合 1」の規則で決められているが、変更されている場合もありえる) を踏襲し、ただし、ソースコードのファイル名部分 (拡張子よりも前の部分) につき、バージョン番号を置換し、かつ yyyymm_abcdef 部分を、今回のタスク実行に応じて、新しいものであることが分かるようにせよ。(ファイル名の例: dnnt_xxxxxxxxxxxxx_functiondesc_r99_yymmdd_abcdef.cs) (ここでの yymmdd は、JST における本タスク実行年月日 6 桁。abcdef は、このタスクの本プロンプトの標題名における「DNNT」の後の数字6桁+ハイフンの後に出現する "英数字大文字 6 桁" を小文字化したもの。) (元の 1 つ古いソースコードのファイル名がこの形式になっていない場合は、ある程度ヒューリスティックに、このルールに従って、趣旨を理解し、新しいファイル名を付けること。)



なお、「dnnt\_」で始まる .cs ファイルが、メインで修正すべきソースコードである ([YGJ9E7ZV] の記載のとおり)。原則としてこのソースコードのみを修正し、新しいソースコードを作成したり、あるいは別のソースコードを修正したりしないこと。ただし、どうしても、別のソースコードがライブラリとして添付されていてそちら側にバグがある場合でその別のソースコードの修正を要する場合は、その旨を特に目立つように history および AI 応答出力プロンプトで表示した上で、最小限度の変更を許可する。



3\. 実装上の工夫・指示

いわゆる最上位レベルのステートメントを使用しないこと。



4\. 結果の応答

結果は、新しい .cs を格納した、.zip ファイルとして応答せよ。.zip ファイル名は、「2.」で決まった .cs ファイル名に「.zip」という拡張子をさらに末尾に付けたものとせよ。



また、標準ライブラリ以外のライブラリを PackageReference で参照する場合は、そのような設定を行なった状態の .csproj ファイルも合わせて生成すること。



5\. 修正タスクの場合 (場合 2. 今回実行するタスクが、初版ではなく、前のソースコードの改良行為である場合)

【AI指示】変更修正に必要なコードのみ必要最小限・局所的にに加筆修正する方針とせよ。ただし適切な判断により必要周辺部分の改良改善整理一貫性実現行為は許容する。どこをどのように修正したか応答本文で詳述。



6\. 作業履歴説明ファイル (dnnt_xxxxxxxxxxxxx_functiondesc_history.md) (以下 history) の作成および更新 [PWXWR8DK]

入力ファイルとして history がない場合は、これを新規作成する。ある場合は、既存の history の末尾追記する。既存のものがあれば、新規のものを作らないこと。

&#x20;   \- history には、まず「###### ============================================================」と記載した後に、次を記載せよ。次の項目は人間が読みやすいように適切に md のマークアップを駆使してサービス精神旺盛に記載すること。

&#x20;       \- 1. 見出し。日時 (YYYY/MM/DD HH:MM:SS (JST) 形式)、「今回の $(VAR_NEW_VERSION)」、本プロンプトの標題名における「DNNT」および後続の 13 文字半角英数記号およびタイトル文字列をそのまま (大文字の場合は大文字のまま)、今回のタスク内容、今回指示された作成または変更点の概要 (要約)、などを簡潔に記載。

&#x20;       \- 1-2. ユーザの指示・仕様が不明瞭で、解釈によって実装内容・結果が変わってしまう場合は、【警告: 曖昧な仕様】としてその概要指摘をする。

&#x20;       \- 2. 今回の作業内容における、「0. 変数」の情報をすべて。

&#x20;       \- 3. AI モデルの情報。バージョン・モデル・思考レベル、セッション開始日時 (JST)、応答生成日時 (JST)、この AI プロンプト (直前) のユーザ入力文字列データなど記載

&#x20;       \- 4. 今回の [$(VAR_NEW_VERSION).spec] すなわちユーザが指示した入力プロンプト (仕様) のテキストデータをそのまま転記。

&#x20;       \- 5. 入力ソースコード理解・読解結果のサマリと詳述。

&#x20;           \- この入力ソースコードは、どのような処理をどのような仕組みで行なっているか、それを簡潔にわかりやすく、かつ、ある程度詳しく記述せよ。

&#x20;           \- [C9HFLA7V] で指示されているとおりの理解において、入力ソースコードのコードとコメントの内容的実質的不一致を発見した場合は、その一覧を簡潔に記載するとともに、問題点を詳述すること。また、それが開発者による意図的な齟齬とみられるのか、それとも誤りであるかも簡潔にわかりやすく、かつ詳細に理由とともに述べること。ただし、当該不一致が、すでに history で過去に言及されていて、それと同じ事柄である場合は、それは記載しないこと (それを記載すると、毎回変更ごとに同じ指摘が記載されるようになってしまうため)。--- [HV52E9G3]

&#x20;           \- [XKAS4D9X] で指示されているとおりの理解において、入力ソースコードのコードと過去の [歴代.spec] (本プロンプトまたは history で供給) との間で不一致がみられる場合は、その一覧を簡潔に記載するとともに、問題点を詳述すること。また、それが開発者による意図的な齟齬とみられるのか、それとも誤りであるかも簡潔にわかりやすく、かつ詳細に理由とともに述べること。ただし、当該不一致が、すでに history で過去に言及されていて、それと同じ事柄である場合は、それは記載しないこと (それを記載すると、毎回変更ごとに同じ指摘が記載されるようになってしまうため)。--- [ZZC2J2NA]

&#x20;       \- 6. 今回のタスクで実施した作業の内容のサマリと詳述。どの要求項目をどのように実装したのかなど、後継が内容・手がかりを理解しやすくするための記録として有用にする。

&#x20;       \- 7. 今回のタスクが、機能追加・修正タスクである場合は、今回、1 つ前のソースコードの、どこをどのように修正したのかの具体的変更点を詳述。必要に応じて要点を md のコードブロックとして記載 (ファイﾙ名、行番号および当該変更部分の包含クラス名・関数名なども記載)

&#x20;       \- 8. 今回のタスクにおいて発生した制約事項、懸念事項、結果として実装不十分な点、あるいは心配事がある点について、サマリおよびその詳細を記述せよ。





【AI指示】スケルトンでなく無エラー完全動作コード生成。重要点日本語コメント。関数・引数・戻値、データ構造もコメント。命名規則： エラーにならない範囲で、Microsoft慣習。ハンガリアン表記禁止・予約語禁止。添付ファイルとして応答。



【AI指示】.cs のプログラム中の上のほうには、新ソースコードのファイル名、プログラムの目的や仕様、動作原理、注意事項などをまとめたヘッダ的コメントを付ける。当該ヘッダ的コメントの最上部には、この指示プロンプトの 1 行目の「DNNT」ではじまる行文字列を変更せずそのまま付ける。このプログラムは生成AIにより生成された旨と、プログラムを生成したAIバージョン・モデル・思考レベル、セッション開始日時(JST)、応答生成日時(JST)を記載。また、当該ヘッダコメントは、当該タスクが修正タスクの場合 (場合 2. 今回実行するタスクが、初版ではなく、前のソースコードの改良行為である場合) である場合は、履歴番号 (バージョン番号) とともに、変更点を、変更日時やその際の DNTT 番号などとともに、サマリとして書き加えていくようにせよ。



【AI指示】非常に重要なので、細かいところまで設計し、最大限にトークンを用いてこの処理を行なうこと。その過程においてプログラムの全体機能およびモジュール間のつながりなども詳細整理されることとなる。



\--- [CODE_RULE:CS_NETFX48_APP_VER2601008] ここまで ---





\--- [TASK_INSTRUCTION] ここから ---





1\. 前提

添付 (.zip でまとめて、あるいはソースコードファイル等が単独で、添付されている) されている、コンソール win32 アプリは、初版として [$(VAR_FIRST_VERSION).specs] の仕様・要件に基づいて実装されたものである。その後、$(VAR_FIRST_VERSION) から順に $(VAR_OLD_VERSION) までバージョンアップされてきて、それぞれ、歴代の変更履歴は、[$(R番号).specs] のとおりとなっている。なお、歴代の変更履歴がこのプロンプトに見あたらない場合あるいは内容が不足している場合は、添付の入力ファイルに history ([CODE_RULE] 参照) がある場合はそこに記載されている場合もあるのでこれを参照すること。



このように、初期版から現在の直近版までの変更履歴などを逐次時系列的に理解することにより、現在のソースコードの仕様の元となった要求内容が相当程度明確に理解可能である。しかしながら、それでも不一致がありえる。また、ドキュメントにはなくソースコードにはある、という機能や挙動もありえる。

その不一致は、人間開発者が手動で開発・改良を行なった場合であって、それがドキュメントに追記されていないというような場合である。

あるいは、過去の AI の間違いによる不一致である可能性もある。そのような場合では、現況のソースコードの内容を優先すること。現況のソースコードの内容は、ドキュメント不十分あるいは不一致の場合、ソースコードのコードおよびコード中のコメント (ただし、コメントはコードを理解するために補佐的に利用すべきであり、コードとコメントが食い違う場合は、コードを優先すること) により理解すること。[C9HFLA7V]

すなわち、$(VAR_OLD_VERSION) までのソースコードと、[$(R番号).specs] (本プロンプトまたは history) のドキュメント内容が部分的に齟齬があるとしても、その部分については、ソースコードの現状を維持することを優先し、ドキュメントの記述が常に正しいとしてソースコード側の修正をすることは、明示的にそのようにせよと指定された場合をのぞき、しないようにすること。 --- [XKAS4D9X]

これらの不一致発見時には [HV52E9G3] および [ZZC2J2NA] に従うこと。

また、これらの不一致に関する原因の理解・解釈が、history に記載されている可能性も高いので、それを参考にして理解を助けるようにせよ。ただし、この点について history の理解が誤っている可能性もそれなりにあるので、鵜呑みにしないようにせよ。



2\. 今回のタスク

今回、$(VAR_NEW_VERSION) を、[CODE_RULE] に従って実装せよ。今回実装したい仕様は、[$(VAR_NEW_VERSION).specs] のとおりである。



3\. 応答データ

(1) 応答データのソースコードおよび history 等のファイル群は、[CODE_RULE] に基づき、添付ファイルとして応答せよ。本文での応答は不要。



(2) [CODE_RULE] に基づき実施された作業のうち、[PWXWR8DK] (history の作成) で用意された内容の AI 的理解における 1,1-2,5,6,7,8 の内容に基づくサマリは、history ファイルだけでなく、AI 応答プロント本文としても応答しユーザに表示せよ。



(3) その他、特異な事柄が発生した場合は、AI 応答プロント本文としても応答しユーザに表示せよ。



(4) AI 作業中の試行内容・作業内容・推論過程は、日本語で、できる限りリアルタイムで表示詳述せよ。



\--- [TASK_INSTRUCTION] ここまで ---



````

# 5. 入力ソースコードの理解・読解

今回は初版であり、入力ソースコード・入力プロジェクト・既存historyは添付されていない。添付はR01の仕様書一つである。従って、既存コードとコメントの不一致、既存コードと歴代仕様の不一致、既存historyとの重複指摘に該当するものはない。過去版の挙動を推測して追加していない。

仕様から必要な処理を、開始前の絶対パス検証・両loopbackの確保・Chrome起動・並列HTTP処理・キー操作に整理した。HTTPは生のTCP socketの上に実装し、HttpListener/HTTP.sys/ASP.NET/Kestrelへ置換していない。HTTP.sysのURL ACL登録や管理者権限は実装上の前提にしていない。

# 6. 実施作業の詳細

## 6-1. モジュールの接続

| クラス | 責務・連携 |
| --- | --- |
| Program | 1個の絶対パスを解釈し、ファイルなら親をrootにする。両待受け開始後にURL表示、Chrome起動、q/e/o操作を行う |
| PlatformSupport | 実行時のWindows判定、完全パスの判定、全IPv4/IPv6のTCP/UDP占有ポート集合をAPIで取得 |
| ChromeLauncher | HKCU/HKLM・32/64bitレジストリ、App Paths、コマンド登録、標準配置を探索し、Chromeへnew-windowとURLを渡す |
| ConsoleUi | ログとプロンプトを同期。リクエスト由来の制御文字を可視化し、キー入力待ちでログを止めない |
| HttpServer | ポート選択、両待受け、接続の管理、Timer監視、resource振り分け、ファイル配信 |
| HttpConnection | 16KiB先読みバッファ、部分送信の繰返し、非同期I/O、実送受信時刻、接続の解放 |
| HttpProtocol | 要求行・Host・各ヘッダ・CL/TE・Expectを検証し、要求本文とtrailerを消費 |
| WebPath / MappedPath | origin/absolute-form、UTF-8 percent復号1回、dot正規化、字句的root制約と公開URL生成 |
| DirectoryListing | indexがないディレクトリの一覧。公開URLのリンク、HTML escape、親、UTC日時、サイズ |
| RangeParser / ByteRange | suffix/open-ended、桁あふれ対策、範囲外判定、重複・隣接範囲の統合 |
| HttpValidators | If-Match、If-Unmodified-Since、If-None-Match、If-Modified-Sinceの優先評価 |
| HttpResponse | HTTP version、status、8固定ヘッダ、Date、Content-Length、Connection、HEAD/204/304の本文抑止 |
| MimeTypes | 指定MIMEの全行をハードコードし、後勝ち辞書・最長拡張子照合を提供 |

## 6-2. 通信・ファイル処理

待受けを127.0.0.1と::1の個別socketとして保持し、IPv6 DualModeを無効にする。片方だけ成功した候補は解放して次へ進む。Nagleは待受け・受理済みの全socketで無効にし、lingerも明示している。

受理後の開始処理をthread poolへ渡すので、同期完了が連続する長いpipelineがaccept loopを占有しない。I/O待ちはTaskで行い、待機接続ごとの専用threadを持たない。1接続内は応答順を保持し、別接続を並列処理する。ファイルはFileShare.ReadWrite/Delete付きで開き、64KiBバッファでseek/read/sendする。サイズはlongで扱い、2GiB/4GiBで切り詰めない。

HTTP/1.1は通常持続、HTTP/1.0は明示Keep-Alive時に持続する。Connection:closeを優先する。要求本文のContent-Length/chunkedを上限内で最後まで消費し、先読みした次要求を捨てない。CLとTEの併記、異なるCL、Host重複、不正なchunk等はエラー後closeとし、メッセージ境界を曖昧にしない。100 Continueの後にエラーが判明した場合も最終エラーを返せる。

ソケットの同期用SendTimeout/ReceiveTimeoutも300秒へ設定するが、非同期I/Oには別の監視が必要なので、monotonic clockで最後の成功した送受信時刻を追跡し、1秒周期のTimerで300秒無通信の接続を閉じる。タイムアウトによる切断ログは出さない。

Windowsでは受理前の相手切断がAcceptExのConnectionReset/Aborted等になる場合を一時エラーとして扱い、サーバー全体を終了しない。HTTP応答を書き始めた後でファイルが短縮された場合などは、二重の応答を継ぎ足さず接続を閉じ、必要なHTTPエラーをコンソールへ出す。

## 6-3. パス・一覧・条件付き要求

URIのpathをqueryから分離し、percent-encodingと元UTF-8バイト列を厳密に一度だけ復号する。rootより上へ出るdot segmentは403。WindowsのADS、予約device名、区切り文字や末尾正規化を使う別名アクセスも拒む。Path.GetFullPath後に区切りを含むroot境界で比較し、単なる文字列prefix一致で別ディレクトリを許可しない。symlink/junctionの実体へcanonicalizeしないことにより、ユーザーが明示的に許可した外部リンクは辿れる。

ディレクトリの末尾slashを補うredirectはqueryを保持する。index4種を順に探し、なければ名前をHTML escapeした一覧を返す。通常ファイルに末尾slashを付けたURIは404。ファイル名の日本語・空白・#・%などはURL segmentとして適切に再符号化する。

通常ファイルの弱ETagはサイズと元mtimeで作る。未来mtimeはLast-Modifiedを応答時点までclampする。一覧は生成本文のSHA-256から弱ETagを作り、子ファイルの更新を反映しないディレクトリmtimeをLast-Modifiedとして使わない。

GETの単一Rangeは206とContent-Range、複数Rangeは正確なContent-Lengthを持つmultipart/byteranges。存在しない範囲だけなら416とbytes */全長、HEADではRangeを無視する。未知単位、不正な範囲構文、16指定を超えるRangeは通常応答へ戻す。If-Rangeは上記の弱validator方針で全体取得へ戻す。

## 6-4. ビルドと検証

[VERIFICATION.md](VERIFICATION.md) に環境、区分別件数、実測タイムアウト、初回差異の原因と修正、ハッシュを記載した。最終の全件JSON/テキスト、サーバーログ、ビルドログ、PE解析記録は `test-results/` に同梱する。

主ソースSHA-256: `080170b1f068a6fe59d62a11b3cd988941bc1397e8014462159139c44f9e06eb`

納品EXE SHA-256: `6882bf6a2cbfba3abd250cb583c2fdb1e1da17ed8a9ebfda0df91d7347183475`

# 7. 具体的変更点

R01の新規作成なので、前版へのdiffや旧ファイルの改変はない。指定規則に従う唯一のC#ファイルを生成し、ロジックを別CSへ分散していない。assembly属性も同じCS内に記載した。プロジェクトの明示的Compileはその1個だけである。README、原仕様、history、試験スクリプト、試験結果は別ファイルで同梱した。

作業中の初回試験では不正なHTTPバージョン文字列を400へ分類する修正を行い、reviewではLast-Modified、一覧の条件付き応答、末尾slash、accept一時切断を補強した。ポート試験の期待値にあったSO_REUSEADDR差はテスト側を修正し、実装側の正しい最小ポート選択を変更していない。

# 8. 制約事項・未検証範囲・残る留意点

- Windows版CLR、Windowsの実レジストリ・IP Helperテーブル、UNC/ジャンクション実体、ChromeのGUIは、このLinux環境では未実機検証である。ユーザー指定に従い、Chromeの起動コード試験は省略した。4.8参照による実コンパイルと同一EXEの互換ランタイムHTTP試験は実施済み。
- NoLingerは即時再利用の絶対保証ではない。Windowsの排他bindが拒否された場合は次候補を使用する。この点を満たす保証まで必要なら、使用OS上の具体的TCP状態と運用条件を含む別要件が必要となる。
- ファイルを同じサイズ・同じmtimeを維持して書き換えると弱ETagが変わらないことがある。Cache-Control:no-storeは常に付けるが、明示的条件要求に対して内容hashと同じ強さを保証しない。If-Rangeは意図したfull-response fallbackである。
- ファイルを送信中に更新/削除/短縮した場合、完全な瞬間スナップショットを提供する仕組みはない。長さ不足等を検出したら接続を終了する。
- 64MiBは受信して読み捨てる要求本文の制限であり、静的ファイルの容量上限ではない。行・ヘッダ・trailer・Range個数にも上限を設けている。
- IPv4/IPv6双方が必須。Chrome不在でもサーバーは継続して表示URLから開ける。Chromeの企業ポリシー等によるウインドウ表示成否は監視しない。
- 128並列や大きいファイルのRange試験は正確性の確認であり、個々のWindows実機の帯域やp99遅延を保証する測定ではない。

# 9. 参照した一次資料

- [RFC 9110: HTTP Semantics](https://www.rfc-editor.org/rfc/rfc9110.html) — メソッド、条件付き要求、Range、弱validator、Last-Modified。
- [RFC 9112: HTTP/1.1](https://www.rfc-editor.org/rfc/rfc9112.html) — 要求文法、framing、chunked、Host、持続接続、absolute-form。
- [W3C: The Original HTTP as defined in 1991](https://www.w3.org/Protocols/HTTP/AsImplemented.html) — HTTP/0.9の本文のみの形式。
- [Microsoft: Socket.LingerState](https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.socket.lingerstate?view=netframework-4.8.1) — linger設定。
- [Microsoft: Socket.ReceiveTimeout](https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.socket.receivetimeout?view=netframework-4.8.1) — 同期受信のタイムアウト。
- [Microsoft: SO_REUSEADDR / SO_EXCLUSIVEADDRUSE](https://learn.microsoft.com/en-us/windows/win32/winsock/using-so-reuseaddr-and-so-exclusiveaddruse) — Windowsでのポート再利用と排他bind。
- [Microsoft: AcceptEx](https://learn.microsoft.com/en-us/windows/win32/api/mswsock/nf-mswsock-acceptex) — 受理前に切断した場合のWSAECONNRESET。
- [Microsoft: GetActiveTcpListeners](https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.ipglobalproperties.getactivetcplisteners?view=netframework-4.8.1) / [GetActiveUdpListeners](https://learn.microsoft.com/en-us/dotnet/api/system.net.networkinformation.ipglobalproperties.getactiveudplisteners?view=netframework-4.8.1) — 全ローカルアドレスのTCP/UDP列挙。
- [Microsoft: Application Registration](https://learn.microsoft.com/en-us/windows/win32/shell/app-registration) — App PathsのChrome探索。
- [Chromium: chrome_switches.cc](https://chromium.googlesource.com/chromium/src/+/c60e98e3f568d5dd5fc158652ba264d3d6647393/chrome/common/chrome_switches.cc) — new-window指定。
- MIME表は本ユーザー入力に含まれるものをそのまま使用し、原表に記載されたgistのURLもCS内へ残した。
