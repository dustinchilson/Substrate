# Substrate

Substrate is a set of file based nuget packages that will serve as building blocks for building applications.

## Installation

Package  | Nuget 
---------|----------
 Substrate.Reliability.CircuitBreaker | [![NuGet](https://img.shields.io/nuget/v/Substrate.Reliability.CircuitBreaker.svg)](https://www.nuget.org/packages/Substrate.Reliability.CircuitBreaker)

### Substrate.Reliability.CircuitBreaker

Substrate.Reliability.CircuitBreaker is an implementation of a basic CircuitBreaker class. It is based on the work of Tim Ross' "Implementing The Circuit Breaker Pattern In C# series" [Part 1](https://timross.wordpress.com/2008/02/10/implementing-the-circuit-breaker-pattern-in-c/) & [Part 2](https://timross.wordpress.com/2008/02/17/implementing-the-circuit-breaker-pattern-in-c-part-2/).

```cs
WorkResult result;
try
{
    var cb = new CircuitBreaker();
    cb.IgnoredExceptionTypes.Add(typeof(TimeoutException));
    result = cb.Execute(() => DoWork());
}
catch (OpenCircuitException ex)
{
    // Fail fast due to previous failures
}
catch (OperationFailedException ex)
{
    // New failure
}
catch (TimeoutException ex)
{
    // Ignored ex
}
```

## Inspirations

Several libraries distribute themselves just as source files that become part of the referencing dll. 

* [SimpleJson](https://github.com/facebook-csharp-sdk/simple-json)
* [TinyIoc](https://github.com/grumpydev/TinyIoC/blob/master/src/TinyIoC/TinyIoC.cs)
* [TinyMessenger](https://github.com/grumpydev/TinyIoC/blob/master/src/TinyMessenger/TinyMessenger.cs)

Several libraries also provide great building blocks for building applications

* [FoundatioFx](https://github.com/FoundatioFx/Foundatio)

## Contributing
Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

Please make sure to update tests as appropriate.

## License
[MIT](https://choosealicense.com/licenses/mit/)
