////import { default as Axios } from "axios";
////import { default as _ } from "lodash";
////import { default as $ } from "jquery";
////import { default as Moment } from "moment";

import { Moment } from "./DnImports";

//Moment

//import "moment/locale/ja";
//Moment.locale("ja");

export class Greeter
{
    public static greet(message: string): string
    {
        return `Hello, ${message}!`;
    }
}

export class TestClass2
{
    public static Hello2(message: string): void
    {
        console.log("Test2 - " + Moment().format("M - D （dd）")) // => 12月３日（日）
    }

    public static GetMoment(): Moment.unitOfTime.All
    {
        return "year";
    }
}

export class TestClass1
{
    public static Hello(message: string): void
    {
        console.log("Hello " + message);
    }

    public static async SleepAsync(msec: number): Promise<void>
    {
        return new Promise(
            function (resolve)
            {
                setTimeout(function ()
                {
                    resolve();
                }, msec);
            }
        );

    }

    public static async HelloAsync(): Promise<void>
    {
        console.log("start");
        for (let i = 0; i < 20; i++)
        {
            if (i >= 10)
            {
                //throw "This is Error !!!";
            }
            await this.SleepAsync(50);
            console.log("Neko_ 200 : " + i);
        }
        console.log("end");
    }
}


