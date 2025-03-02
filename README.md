# Vintage Story Play To Earn
Base template for creating a vintage story compatibility with PTE coin, check commands in [wiki](https://github.com/GxsperMain/vs_play_to_earn/wiki)

### Dependencies
- [AFKModule](https://mods.vintagestory.at/afkmodule)
- [Mysql Database](https://www.mysql.com/)

# Using
- Install a database like mysql or mariadb
- Create a user for the database: GRANT ALL PRIVILEGES ON pte_wallets.* TO 'pte_admin'@'localhost' IDENTIFIED BY 'supersecretpassword' WITH GRANT OPTION; FLUSH PRIVILEGES;
- Create a table named vintagestory:
```sql
CREATE TABLE vintagestory (
    uniqueid VARCHAR(255) NOT NULL PRIMARY KEY,
    walletaddress VARCHAR(255) NOT NULL,
    value DECIMAL(50, 0) NOT NULL DEFAULT 0
);
```
- Put this mod on your server, [reference](https://wiki.vintagestory.at/Adding_mods)
- Starts the server at least one time
- Go to DataPath from your vintage story server, find ModConfig and change the PlayToEarn configurations for you desired database
- Restart the server and everthing should be running

# About VS Play To Earn
VS Play To Earn is open source project and can easily be accessed on the github, all contents from this mod is completly free.

If you want to contribute into the project you can access the project github and make your pull request.

You are free to fork the project and make your own version of VS Play To Earn, as long the name is changed.

# Building
- Install .NET in your system, open terminal type: ``dotnet new install VintageStory.Mod.Templates``
- Create a template with the name ``PlayToEarn``: ``dotnet new vsmod --AddSolutionFile -o PlayToEarn``
- [Clone the repository](https://github.com/GxsperMain/vs_play_to_earn/archive/refs/heads/main.zip)
- Copy the ``CakeBuild`` and ``build.ps1`` or ``build.sh`` and paste inside the repository

Now you can build using the ``build.ps1`` or ``build.sh`` file

FTM License