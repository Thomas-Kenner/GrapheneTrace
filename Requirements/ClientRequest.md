# Graphene Trace

Graphene Trace is a MedTech startup based in Chelmsford, developing Sensore: continuous & automated pressure ulcer prevention using e-textile pressure mapping and ‘smart alert’ AI analysis.

# Data Format

Sensore pressure mapping sensors generate real-time pressure distribution heat maps, formatted as a time-ordered array of 32x32 matrices. Data for this live brief project will be formatted as a series of csv files (figure 1): separated by user ID and time/date.

Values in the database range from 1-255 according to pressure applied to the corresponding sensor pixel, with 1 being the default zero-force value, scaling linearly with pressure to saturation at 255.

Figure 1: Excerpt of sensor data from csv file, showing data formatted into 32 columns with 32 rows per ‘frame’

Figure 2: a) example ‘live’ heat map output from sensore mat, b) sensore mat on wheelchair, c) Sensore app dashboard displaying real-time heat map, key metrics and graphs extracted from the data, with a timeline scrubber to interrogate previous data

# User Requirements


    Establish database structure to format time-ordered data per user.

    Tree classes of user login: user (patient), clinician and admin. The clinician login should have access to all/select groups of individual users’ data. The admin login should create accounts for the other types of users.

    Analyse pressure map data to identify high pressure regions and generate an alert for the user, while flagging that period within the database for further review by clinician

    Extract key metric information from the dataset:
        Peak Pressure Index: highest recorded pressure in the frame, excluding any areas of less than 10 pixels
        Contact Area %: percentage of pixels above lower threshold, indicating how much of the square sensor mat is covered by the person sitting on it.

    Graphs / methods of visualising how those key metrics change over time, with user selectable time periods: over last hour, last 6h, 24h etc.

    Generate reports showing a user-friendly way of displaying key information from the data, and a comparison to previous dataset: day-to-day change, this hour yesterday etc.

    System for user input and feedback on a dashboard that displays the heat map data: box for comments submission that is associated on the back-end with that particular timestamp of pressure map - useful for flagging risk regions by the user. Additionally, a method for the clinician to review user comments and reply in-thread.

The company also has some “nice to have” requirements:

    Extract from the dataset other key metric information than the ones at point 4
    Enhance the visualisation methods with an emphasis on aesthetics and user experience, ensuring that the data is represented in a way that is easily understandable to a lay audience.
