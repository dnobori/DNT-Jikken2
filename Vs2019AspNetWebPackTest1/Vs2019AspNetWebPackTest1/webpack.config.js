const path = require("path");

const config = {
	mode: "development",
    devtool: "inline-source-map",
    // webpack���o���h���̍\�z���J�n����G���g���|�C���g
    entry: path.resolve(__dirname, "Scripts/DnApp.ts"),
    output: {
        // �o�͂���t�@�C����
        filename: "bundle.js",
        // �o�̓t�H���_
        path: path.resolve(__dirname, "wwwroot/js")
    },
    module: {
        rules: [
            // TypeScript���������郍�[�_�[
            { test: /\.ts$/, loader: "ts-loader" }
        ]
    },
    resolve: {
        extensions: [".ts", ".js"],
        // ���W���[����T���t�H���_�inode_modules��scripts�t�H���_��Ώۂɂ���j
        modules: [
            "node_modules",
            path.resolve(__dirname, "scripts")
        ]
    }
};

module.exports = config;
