/// <binding Clean='Run - Development' ProjectOpened='Watch - Development' />
const path = require("path");

const config = {
	mode: "development",
    devtool: "inline-source-map",
    entry: path.resolve(__dirname, "Scripts/DnApp.ts"),
    optimization: {
        moduleIds: 'deterministic',
    },
    output: {
        filename: "bundle.js",
        path: path.resolve(__dirname, "wwwroot/js")
    },
    module: {
        rules: [
            { test: /\.ts$/, loader: "ts-loader" }
        ]
    },
    resolve: {
        extensions: [".ts", ".js"],
        modules: [
            "node_modules",
            path.resolve(__dirname, "scripts")
        ]
    }
};

module.exports = config;
