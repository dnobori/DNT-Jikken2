export class Greeter
{
    public static greet(message: string): string
    {
        return `Hello, ${message}!`;
    }
}

export class TestClass2
{
    public static Hello2(message: string)
    {
        console.log("Hello " + message);
    }
}

export class TestClass1
{
    public static Hello(message: string)
    {
        console.log("Hello " + message);
    }

    public static async SleepAsync(msec: number)
    {
        return new Promise(
            function (resolve, reject)
            {
                setTimeout(function ()
                {
                    //throw "Hello Error";
                    //reject("neko");
                    resolve(0);
                },
                    msec);
            }
        );

    }

    public static async HelloAsync()
    {
        console.log("start");
        for (let i = 0; i < 20; i++)
        {
            if (i >= 10)
            {
                throw "This is Error !!!";
            }
            await this.SleepAsync(50);
            console.log("Neko_ 7810 : " +i);
        }
        console.log("end");
    }
}


