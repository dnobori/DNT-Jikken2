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
        path: path.resolve(__dirname, "wwwroot/js"),
        library: {
            name: "Hoge",
            type: "umd",
        }
    },
    target: ['web', 'es3'],
    module: {
        rules: [{
            test: /\.ts$/,
            loader: "ts-loader",
            include: path.join(__dirname, "Scripts"),
        }]
    },
    resolve: {
        extensions: [".ts", ".js"],
        modules: [
            "node_modules",
            path.resolve(__dirname, "Scripts")
        ]
    }
};

module.exports = config;
