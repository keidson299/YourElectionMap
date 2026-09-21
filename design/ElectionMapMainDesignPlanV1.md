# Election Map Website

## Purpose

This website serves as a way for people to see exactly which election maps they are within. This stretches from federal election maps, through state maps, all the way down to municipal boundaries for elections like school board or city council members.

## Function

The website fulfills its purpose through two major components that communicate with each other: A front-end client website that a user interacts with, and a back-end server app that will handle all the BL.

### Front End

The front-end client is the way users interact with the site. While on the site, users will see a map of the US (see deflock) and be able to insert an address into a search bar (similar to what is seen on sites like Apartments.com). After typing in their address and hitting enter, the site focuses the map location to the address entered, and allows the user to overlay the their election maps onto the US map. If possible, there should also be basic information on any upcoming elections being held in that map. Such information should include: Candidate names, political affiliation, candidate websites, propositions, date of the previous and upcoming elections, and (if the election has passed) the published election results. This information is sent to the client via the back-end server.

### Back End

Second, the back-end server (C# API Gateways, hosted on whatever cloud-hosting service is cheapest). The backend has one client facing endpoint: GetElectionInfoBasedOnAddress. This endpoint receives the address the user typed into the client, and performs the logic to determine the election information to use for the given address and return back those maps, candidate, and election information. The map, candidate, and other election information is stored in a cloud database. That database is updated by a serverless function that queries the info from publicly accessible government records.

## Client Design & Structure

The front-end client is built using React. This was chosen mainly due to my familiarity with it, as well as it being a widely used and popular language for creating websites, especially for smaller scale projects like mine. There may be some future investigations into potentially using Angular if React can’t give me the functionality that is needed, or into Go if speed is a major concern. However, with the simplicity of the app, it is doubtful that highly specific functionality would be needed, or that the React rendering would be the major speed bottleneck.

The site has a simple layout, a map of the US as the background with the search bar hovering above it in the middle of the page. This will allow for easy use across various different screen sizes and dimensions. After a user has submitted an address, the layout changes only slightly as the background map becomes a movable screen element to allow the user to see and interact with all the maps. There is also a sidebar (will open by default on desktop and stay hidden on mobile) that displays all the relevant election information, broken down into federal, state, and local sections for greater legibility.

The user can input as many different addresses as they would like, so long as they are valid addresses within the US. On the client side, a first pass is run to ensure that the sent address is a valid US address. Should the address fail this pass, the site will display an error requesting the user to enter a valid US address.
There is no login or other screens.

## Server Design & Structure

The back-end server is built using C# and Entity Framework. Similar as to the explanations given for the client’s structure: Both the language and tool have great familiarity and a wealth of resources and information to help address any issues that come up with the app.

The server has a single endpoint GetElectionInfoBasedOnAddress. This is a simple HTTP Get/Post method, that takes in a structured address and performs these steps:

1. Confirm that the sent address is a valid US address
2. Use the address to determine the maps to return
   a. Start with State, then Zip Code, then City, and finally Street Address to whittle down the number of maps
   b. Should there be an issue where the Street Address is in an unincorporated area, remove the City from the whittling
3. Using the determined election maps, collate the other election information (candidates, upcoming elections, etc.)
4. Return HTTP success back along with a structured object containing all the collected election information
5. Should any step in this process fail or error, return HTTP error back to the user

There is also a serverless function that runs on a fixed schedule, to query the relevant governmental offices to grab the most up-to-date election information. Maps are updated once a year (or manually in the case of an out-of-season map change), while candidate information is updated daily/weekly. A serverless function was chosen as it is both the easiest to setup and implement with the necessary schedule as well as the cheapest to operate compared to running a standalone server.

The maps and election information are stored on a database in the cloud alongside the serverless function. There are two options for the structure: Use a key-value db to store the maps and a relational db to store the election information, or use a single key-value db for both. Having two separate dbs does increase complexity and decrease maintainability, but provides more structure to the entirely text based election information and could provide cost and space savings. Neither db setup stores any user data, the only data stored is publicly available election information. This keeps data security risks at a minimum and means that the storage requirements are static to a scaling user base.
