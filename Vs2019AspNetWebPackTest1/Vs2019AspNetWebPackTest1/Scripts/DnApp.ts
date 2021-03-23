import "core-js/es/promise";

import { Greeter, TestClass1, TestClass2 } from "./DnLib";

import Guacamole from "guacamole-common-js";
//import Guacamole from "./guacamole-common-js";


//console.log("Hello World");

//alert(Greeter.greet("world 021"));

TestClass2.Hello2("Inu");
TestClass1.Hello("Neko");

//TestFunc1();


function TestFunc1(): void
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
        console.log("Tom Html test 1");
    }

    public static GuacamoleTest1(display: HTMLElement): void
    {
        const tunnel = new Guacamole.WebSocketTunnel("Model.WebSocketUrl");

        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        tunnel.onerror = function (status: any): void
        {
            console.log(status);
            alert("Tunnel Error Code: " + status.code);
        };

        // Instantiate client, using a WebSocket tunnel for communications.
        const guac = new Guacamole.Client(tunnel);

        // Add client to display div
        display.appendChild(guac.getDisplay().getElement());

        // Error handler
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
}

export default function sampleFunctionExported1(): void
{
    console.log("sampleFunctionExported1");
}

export function TestFunc2(): void
{
    console.log("TestFunc2");
}
