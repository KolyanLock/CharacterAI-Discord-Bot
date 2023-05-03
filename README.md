# CharacterAI Discord Bot
I added a message buffer and now the character "sees" the whole chat. I also added a timer for the character to write when there is no activity in the chat and a message counter when there is no activity. DEAD_CHAT_MESSAGE - no one has written a message for how many minutes.
An unofficial Discord integration with a CharacterAI service which you can use to add any character on your own Discord server.

**For use Dockerfile:
1build publish net7.0 linux x64 framework-dependent Produce single file. 
2. Copy the Confil.json file to the folder to the folder with publish build. 
3. Create folder temp in the folder with publish build then copy folder storage to the temp folder ( it should turn out like this /temp/stprage). 
4. Copy Dockerfile to the folder with publish build.**


For use Dockerfile build publish fre
An unofficial Discord integration with a [CharacterAI](https://beta.character.ai/) service which you can use to add any character on your own Discord server.

<div align="center">
    
![Logo](https://user-images.githubusercontent.com/55811932/224168501-48e81f64-9b2f-442c-a8fe-6ecab8d7aab2.png)<br>
<!-- ![Logo](https://user-images.githubusercontent.com/55811932/226441262-8edbb834-33d5-4cd2-8fac-0bacfa6ff79b.png) -->

<sup><b>If you found this project useful, the best way to thank me is to simply leave a star ⭐<br>
[Discord Server](https://github.com/drizzle-mizzle/CharacterAI-Discord-Bot/discussions/22#discussioncomment-5502307) | Based on [CharacterAI.Net](https://github.com/drizzle-mizzle/CharacterAI.Net)</b></sup>
</div>

## 🪄Features
- Talk with any character on your own server and change them on the fly.
- Embedded character search.
- Automatically sets the name and profile picture of a character.
- Supports answers swiping and image sending.
- Supports multiple parallel chats with the same character.
- Random replies, audience mode and some other stuff.

    ![chrome_cshYMsGIaB](https://user-images.githubusercontent.com/55811932/211129383-c7cd4ca2-ceb4-42c5-8449-bc6ce9b2d538.gif)
    
## 📓Documentation
- [How to set up](https://github.com/drizzle-mizzle/CharacterAI-Discord-Bot/wiki/How-to-set-up)
- [Commands](https://github.com/drizzle-mizzle/CharacterAI-Discord-Bot/wiki/Commands)
- [Additional configuration](https://github.com/drizzle-mizzle/CharacterAI-Discord-Bot/wiki/Additional-configuration)

<!-- ## 🩼Known issues
Some **Windows users** are facing the problem of being unable to access character.ai: https://github.com/drizzle-mizzle/CharacterAI-Discord-Bot/issues/28 <br>
If you encounter something similar to:<br>
> `Request failed! (https://beta.character.ai/chat/character/info/)`<br>
> `Response: Forbidden`

First, make sure that you use correct cAI user token.<br>
If it's correct, but you're still getting this error:
- Try to use some VPN. 
- If there's still no luck, or if you're not comfortable with using VPN, sadly, **the only available workaround right now is to use Linux OS** for hosting.<br>
The simplest way is to just install WSL2 with Ubuntu, which is pretty easy to do, and launch this bot from there:<br>
[How to Install Ubuntu on WSL2 on Windows](https://ubuntu.com/tutorials/install-ubuntu-on-wsl2-on-windows-10#1-overview)
(Additional guide for ducklings: https://github.com/drizzle-mizzle/CharacterAI-Discord-Bot/issues/41)
-->
