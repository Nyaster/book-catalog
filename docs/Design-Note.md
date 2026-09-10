# Book Catalog Platform — Design Note

> Week one decithions.

This week i build small CRUD api for our book. 
There few operation you can do.

POST /books -- Create a new book

Get /books -- Get paginated and filtered boooks(optional) books

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

> Week two decithions.

# How you split the layers and why exactly this way

There is simple standart layered architecutre, i explained this in week one. 
For more details. 
Domain -> Answer to simpliest entities, which represent our 'product', his layer is independend from any other layer.

Infrastructure -> Is abstraction of how we store our data. In future, if we need to change DB, we only need to change this layer.

Application -> Is abstraction of how we process business logic, if we need calculation, or do some logic, this is going here.

Presentation -> Is abstraction of how we present data to user. If we need to present data in different way, this changes to go here, even more. There may be more than one api layer in future.

# What your data access abstraction looks like and what it hides

I have `IBookRepository` which hides how we store data. This provides all crud operations, with pagination and filtering support.

Currently i implement it with in memory storage, in future i will replace it with real database. with no changes in application layer.


# How you decided what to test and what not to test

I am not very good at testing, this might be weakest from my part in this week.

Unit test are need to check small parts of code. In ideal world, we need to have cover all code, with all possible inputs.
But in real world, we test only intended behavior. (Legal inputs, illegal inputs, Expected errors). 
So it's why i write unit test for business logic, and trying to cover all intended behavior.

# What was painful to change from week 1, and what that tells you about your original design
Due to how i start write application from begginning there near zero changes in layers and arhictecture, only few changes in contracts to add pagination and filtering features.


> Week three decithions.

# Your data model and why it is shaped this way

Entity | Why it is shaped this way
--- | ---
Book | Represent borrowable book, and it's catalog details.
Author | Represents Author, Author can write multiple books. It's helping Books refereence same person.
User | Represents user, which can borrow book.
Loan | Records one borrowing event: which book, which user, when borrowed and when returned.

To able story borrowing history, loan is separate entity.
In future if book can be in more than one copy, we can simply extend this via this many to many relationship.
In example add new column `Copies` to `Book` entity which represent how many copies of book are available.
Or we make another entity `BookCopy` which represent one copy of book, so we can have borrowing story for each copy.(If this system represents real library).

# Which database you chose and why
I choose PostgreSQL. It's modern, fast, and very good document database. I have experience with it, and it's why i choose it.

# How much of your code had to change when you replaced in-memory storage, and what that says about week 2
Not so much, most of changes due to changes in contracts and introducting new entities and it's relationships.

# Where you expect performance to become a problem first
Mostly of the filtering and pagination. I use simple offset pagination, which in big amount of data is not good performance-wise.
If we need to support large amount of data, i would use token based pagination, which is more efficient but need a little more effort to implement.

# What each meaningful line of your Dockerfile does
- FROM -- Base image
- COPY -- Copy files from local machine to docker image
- RUN -- Run command in docker image
- EXPOSE -- Expose port to outside world
- ENTRYPOINT -- Entrypoint of docker image