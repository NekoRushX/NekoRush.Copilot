## NekoRush.Copilot.BuildTools

This tool enables you to generate the code via ChatGPT in the build time from the *.prompt file.  
Let ChatGPT release your mind, and take time to enjoy a cup of coffee.  

~~Although I don't ensure it always can give you a correct code. XD~~

## How to use the buildtool

Only two targets you should take care about: `CopilotApi` and `CopilotGenerate`.  
CopilotApi declares the ChatGPT api and the apikey, use which [model](https://platform.openai.com/docs/models/overview) to process the prompt.  
CopilotGenerate reads your prompt file and request the api, then compiles them.

```
<ItemGroup>
	<CopilotApi Remove="None" Api="https://api.openai.com/v1/chat/completions" ApiKey="<YOUR API KEY>" Model="gpt-3.5-turbo-1106" />
	<CopilotGenerate Include="Commands\Add.prompt" />
	<CopilotGenerate Include="Commands\Echo.prompt" />
	<CopilotGenerate Include="Main.prompt" />
</ItemGroup>
```

## LICENSE
Licensed under MIT license with ❤
