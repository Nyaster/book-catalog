# Book Catalog Platform — Design Note

> Week one decithions.

This week i build small CRUD api for our book. 
There few operation you can do.

POST /books -- Create a new book

Get /books -- Get all books

Put /books/:Guid -- Update a book

Delete /books/:Guid -- Delete a book

# Structure
I Use simple four layer architecture.
Domain is the core of entire application.
After that is going infrastructure layer, which answer to question like how to store data.
Then is going to application layer, which answer to question like how process business logic.
Finally is going to presentation layer, which answer to question like how to present data to user.

# Decisions
For storage, i use in memory storage, simple dictionary, using locs for handling concurrency. For current application state this is simpliest and straitghtforward way to store data.

For logging i use build in logger. Simple and straight forward. For future it's been easily replaced with any other logging library without any change in code.

For error handling i use global exception handler, which currentl handle all exceptions.

# What to imporve
 - Add tests
 - Real DataBase would be better 
 - Maybe automapping?

# Hard things 
 - For this week, there really most of thing simple, but i found strange behavior when swagger don't get proper errors messages, and i need to write small workaround. This is maybe because i don't use swagger gen, and reuse openapi documentation.

