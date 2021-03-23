import "core-js/es/promise";
import { TestClass1, TestClass2 } from "./DnLib";
import Guacamole from "guacamole-common-js";
//import * as Guacamole from "./guacamole-common";
//console.log("Hello World");
//alert(Greeter.greet("world 021"));
TestClass2.Hello2("Inu");
TestClass1.Hello("Neko");
//TestFunc1();
function TestFunc1() {
    console.log("--a");
    var task = TestClass1.HelloAsync();
    task["catch"](function (x) {
        alert(x);
    });
    console.log("--b");
}
var Tom = /** @class */ (function () {
    function Tom() {
    }
    Tom.HtmlTest1 = function () {
        console.log("Tom Html test 1");
    };
    Tom.GuacamoleTest1 = function (display) {
        var tunnel = new Guacamole.WebSocketTunnel("Model.WebSocketUrl");
        // @ts-ignore
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        tunnel.onerror = function (status) {
            console.log(status);
            alert("Tunnel Error Code: " + status.code);
        };
        // Instantiate client, using a WebSocket tunnel for communications.
        // @ts-ignore
        var guac = new Guacamole.Client(tunnel);
        // Add client to display div
        display.appendChild(guac.getDisplay().getElement());
        // Error handler
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        guac.onerror = function (status) {
            console.log(status);
            alert("Remote Desktop Error Code: " + status.code);
        };
        guac.connect("id=Model.SessionId");
        window.onunload = function () {
            guac.disconnect();
        };
    };
    return Tom;
}());
export { Tom };
export default function sampleFunctionExported1() {
    console.log("sampleFunctionExported1");
}
export function TestFunc2() {
    console.log("TestFunc2");
}
//# sourceMappingURL=DnApp.js.map