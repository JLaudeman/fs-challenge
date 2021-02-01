CREATE TABLE Testss (test_uid integer primary key asc, sTime dateTime, PlaneID varchar(100) NOT NULL, Operator varchar(100) NULL);
CREATE TABLE Measurements (measurement_uid integer primary key, test_uid integer, x real, y real, height real, FOREIGN KEY(test_uid) REFERENCES Tests(test_uid));
