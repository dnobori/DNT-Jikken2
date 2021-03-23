import "core-js/es/promise";

import { Greeter, TestClass1, TestClass2 } from "DnLib";

//console.log("Hello World");

//alert(Greeter.greet("world 021"));

TestClass2.Hello2('Inu');

TestClass1.Hello("Neko");

TestFunc1();

function TestFunc1()
{
    console.log("--a");
    const task = TestClass1.HelloAsync();
    task.catch(x =>
    {
        alert(x);
    });
    console.log("--b");
}

