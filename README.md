# replication-faq
A FAQ on SQL Server Replication

# Build Webpack projects
- CD to root of the project
- Restore all Node packages with:
```sh
$ yarn install
```
- Build and watch changes in the theme project with:
```sh
$ yarn workspace replication-faq-theme run watch
```
- Then reload your website by pressing F5.


# Azure storage account 
- Add `OrchardCore__OrchardCore_Media_Azure__ConnectionString` to App Service configuration

**Note** a default Linux app service or a custom Linux container, any nested JSON key structure in the app setting name like ApplicationInsights:InstrumentationKey needs to be configured in App Service as ApplicationInsights__InstrumentationKey for the key name. 
In other words, any : should be replaced by __ (double underscore).

dotnet add reference ../Modules/Codesanook.OrganizationProfile/Codesanook.OrganizationProfile.csproj

Add a module to a theme
dotnet add reference ../../Modules/Codesanook.OrganizationProfile/Codesanook.OrganizationProfile.csproj


- Name
Replication FAQ
- Recipe
Blog
- Database type
Sql Server
- Connection string
Server=localhost, 1433; Database=replication-faq; User Id=sa; Password=12345Abc$
- user name
admin
- email
admin@replicationfaq.com
- password
12345Abc$

Enable 
Replication FAQ theme


az group create --location eastus2 --name replication-faq-resource-group

  sku B1



