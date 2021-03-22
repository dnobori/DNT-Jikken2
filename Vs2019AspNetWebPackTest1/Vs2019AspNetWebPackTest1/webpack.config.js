const path = require("path");

const config = {
	mode: "development",
    devtool: "inline-source-map",
    // webpackがバンドルの構築を開始するエントリポイント
    entry: path.resolve(__dirname, "Scripts/DnApp.ts"),
    output: {
        // 出力するファイル名
        filename: "bundle.js",
        // 出力フォルダ
        path: path.resolve(__dirname, "wwwroot/js")
    },
    module: {
        rules: [
            // TypeScriptを処理するローダー
            { test: /\.ts$/, loader: "ts-loader" }
        ]
    },
    resolve: {
        extensions: [".ts", ".js"],
        // モジュールを探すフォルダ（node_modulesとscriptsフォルダを対象にする）
        modules: [
            "node_modules",
            path.resolve(__dirname, "scripts")
        ]
    }
};

module.exports = config;
