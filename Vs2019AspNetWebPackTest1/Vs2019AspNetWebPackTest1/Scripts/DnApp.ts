require('./mystyles.scss');

// Web App
import "core-js/es/promise";
import "@fortawesome/fontawesome-free/js/all";
import "prismjs";
import "prismjs/components/prism-json";
import "prismjs/components/prism-bash";
import "prismjs/plugins/line-numbers/prism-line-numbers";
import "prismjs/plugins/autolinker/prism-autolinker";
import "prismjs/plugins/command-line/prism-command-line";
import "prismjs/plugins/normalize-whitespace/prism-normalize-whitespace";
import "buefy";

// Codes
import { default as Axios } from "axios";
import { default as _ } from "lodash";
import { default as $ } from "jquery";
//import { default as Moment } from "moment";
//import "moment/locale/ja";
//Moment.locale("ja");

let d1 = new Date(2017, 0, 1);

//console.log("Test1 - " + Moment().format("M - D （dd）")) // => 12月３日（日）


// @ts-ignore
Prism.plugins.NormalizeWhitespace.setDefaults({
    'remove-trailing': true,
    'remove-indent': false,
    'left-trim': true,
    'right-trim': true,
    'indent': 0,
    'remove-initial-line-feed': false,
});

//Prism.plugins.customClass.prefix('prism-');

import { Greeter, TestClass1, TestClass2 } from "./DnLib";

TestClass2.Hello2("neko");

let abc = TestClass2.GetMoment();


import Guacamole from "guacamole-common-js";


//import * as Guacamole from "./guacamole-common";


//console.log("Hello World");

//alert(Greeter.greet("world 021"));

TestClass2.Hello2("Inu");
TestClass1.Hello("Neko");

//TestFunc1();

function x(): void
{ }

export function TestFunc1(): void
{
    console.log("--a");
    const task = TestClass1.HelloAsync();
    task.catch(x =>
    {
        alert(x);
    });
    console.log("--b");
}

export class Tom
{
    public static HtmlTest1(): void
    {
        Tom.HtmlGetTestAsync();
    }

    public static GuacamoleTest1(display: HTMLElement): void
    {
        const tunnel = new Guacamole.WebSocketTunnel("Model.WebSocketUrl");

        // @ts-ignore
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        tunnel.onerror = function (status: any): void
        {
            console.log(status);
            alert("Tunnel Error Code: " + status.code);
        };

        // Instantiate client, using a WebSocket tunnel for communications.
        // @ts-ignore
        const guac = new Guacamole.Client(tunnel);

        // Add client to display div
        //display.appendChild(guac.getDisplay().getElement());

        // Error handler
        // @ts-ignore
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        guac.onerror = function (status: any): void
        {
            console.log(status);
            alert("Remote Desktop Error Code: " + status.code);
        };

        guac.connect("id=Model.SessionId");

        window.onunload = function (): void
        {
            guac.disconnect();
        }
    }

    public static async HtmlGetTestAsync(): Promise<void>
    {
        console.log("#1");
        try
        {
            const html = await Axios.get("/");

            console.log(html);
        }
        catch (e)
        {
            console.log(e);
        }
        console.log("#2");
    }
}

export default function sampleFunctionExported1(): void
{
    console.log("sampleFunctionExported1");
}

export function TestFunc2(): void
{
    console.log("TestFunc2");
}
