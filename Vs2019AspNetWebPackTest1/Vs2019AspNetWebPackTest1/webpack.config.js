/// <binding Clean='Run - Development' ProjectOpened='Watch - Development' />
const path = require("path");
const MiniCssExtractPlugin = require('mini-css-extract-plugin');

// https://www.npmjs.com/package/webpack-utf8-bom
var BomPlugin = require('webpack-utf8-bom');

// From: https://bulma.io/documentation/customize/with-webpack/
module.exports = {
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
        rules: [
            {
                test: /\.ts$/,
                loader: "ts-loader",
                include: path.join(__dirname, "Scripts"),
            },
            {
                test: /\.scss$/,
                use: [
                    MiniCssExtractPlugin.loader,
                    {
                        loader: 'css-loader'
                    },
                    {
                        loader: 'sass-loader',
                        options: {
                            sourceMap: true,
                            // options...
                        }
                    }
                ]
            }]
    },
    resolve: {
        extensions: [".ts", ".js"],
        modules: [
            "node_modules",
            path.resolve(__dirname, "Scripts")
        ]
    },
    plugins: [
        new MiniCssExtractPlugin({
            filename: 'css/mystyles.css'
        }),
        new BomPlugin(true),
    ]
};


