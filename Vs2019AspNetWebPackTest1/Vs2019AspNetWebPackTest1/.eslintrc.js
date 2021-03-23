module.exports = {
    "extends": [
        "eslint:recommended",
        "plugin:@typescript-eslint/eslint-recommended",
        "plugin:@typescript-eslint/recommended"
    ],
    "plugins": [
        "@typescript-eslint"
    ],
    "env": { "node": true, "es6": true },
    "parser": "@typescript-eslint/parser",
    "parserOptions": {
        "ecmaVersion": 2018,
        "tsconfigRootDir": __dirname,
        "project": ["./tsconfig.json"]
    },
    "ignorePatterns": ["**/*.js"],
    "rules": {
        "quotes": ["warn", "double"],
        "no-unused-vars": "warn",
        "@typescript-eslint/no-unused-vars": "off",
    }
};
