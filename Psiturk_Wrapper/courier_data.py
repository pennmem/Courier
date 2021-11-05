import sys
from sqlalchemy import create_engine, MetaData, Table
import json
import pandas as pd
import json
import numpy as np
import matplotlib.pyplot as plt

db_url = "mysql+pymysql://ckeane1:test_pass@localhost:3306/test_db"
table_name = 'courier'
data_column_name = 'datastring'
# boilerplace sqlalchemy setup
engine = create_engine(db_url)
metadata = MetaData()
metadata.bind = engine
table = Table(table_name, metadata, autoload=True)
# make a query and loop through
s = table.select()
rows = s.execute()

data = []
#status codes of subjects who completed experiment
statuses = [1,3,4,5,7]
# if you have workers you wish to exclude, add them here
exclude = ["debugXCOD2L"]
for row in rows:
    # only use subjects who completed experiment and aren't excluded
    if row['status'] in statuses and row['workerid'] not in exclude:
        data.append(row[data_column_name])

# Now we have all participant datastrings in a list.
# Let's make it a bit easier to work with:

# parse each participant's datastring as json object
# and take the 'data' sub-object
data = [json.loads(part)['data'] for part in data]

# insert uniqueid field into trialdata in case it wasn't added
# in experiment:
# for part in data:
#     for record in part:
#         record['trialdata']['uniqueid'] = record['uniqueid']

# flatten nested list so we just have a list of the trialdata recorded
# each time psiturk.recordTrialData(trialdata) was called.
data = [record['trialdata'] for part in data for record in part]

# data = [json.loads(record['trialdata']) for part in data for record in part]

# Put all subjects' trial data into a dataframe object from the
# 'pandas' python library: one option among many for analysis
data_frame = pd.DataFrame(data)

delivery_days = [0,1,2,3,4];
for day in delivery_days:

    item_list = []
    store_list = []
    store_dict = {}
    item_time = []
    free_recall_list = []
    free_recall_time = []
    cued_recall_list = []

    for row in range(len(data_frame)-2):
        report = data_frame[0][row]
        convert_to_json = report.replace("'", "\"")
        report_dict = json.loads(convert_to_json)

        data_dict = report_dict["data"]

        # look up for the "trial number" key and pull out specific day info
        if (data_dict.get("trial number") != None) and (data_dict["trial number"] == day):

            item = data_dict.get("item name")
            if item != None:
                item_list.append(item)
                store_list.append(data_dict["store name"])
                store_dict[data_dict["store name"]] = item
                item_time.append(report_dict["time"])

            item_recalled = data_dict.get("typed response")
            cued_store = data_dict.get("store displayed")
            if item_recalled != None:
                if cued_store == None:
                    free_recall_list.append(item_recalled)
                    free_recall_time.append(report_dict["time"])

                else:
                    cued_response = [cued_store, store_dict[cued_store], item_recalled, report_dict["time"]]
                    cued_recall_list.append(cued_response)

    # make first time as 0 and subtract the rest
    item_time[:] = [(item - item_time[0]) / 1000 for item in item_time]
    free_recall_time[:] = [(time - free_recall_time[0]) / 1000 for time in free_recall_time]

    # create a pandas dataframe
    free_recall_dict = {"list of item" : item_list, "time displayed" : item_time,
                   "items recalled" : free_recall_list, "time recalled": free_recall_time}
    free_recall_df = pd.DataFrame.from_dict(free_recall_dict, orient="index")

    # cued recall needs additional scriptedEventReporter for store display info (time)
    cued_recall_df = pd.DataFrame(cued_recall_list,
                                  columns=["store name", "correct item", "typed item", "time"])

    pd.set_option('display.max_columns', None)
    print("Free recall for day {}".format(day))
    print(free_recall_df)
    print("\n\n")
    print("Cued recall for day {}".format(day))
    print(cued_recall_df)
    print("\n\n")

#final store & object recall
final_store_recall = []
final_store_time = []
final_object_recall = []
final_object_time = []

for row in range(len(data_frame)-2):
    report = data_frame[0][row]
    convert_to_json = report.replace("'", "\"")
    report_dict = json.loads(convert_to_json)

    if report_dict["type"] == "final store recall":
        data = report_dict["data"]
        final_store_recall.append(data["typed response"])
        final_store_time.append(report_dict["time"])

    if report_dict["type"] == "final object recall":
        data = report_dict["data"]
        final_object_recall.append(data["typed response"])
        final_object_time.append(report_dict["time"])

final_store_time[:] = [(time - final_store_time[0]) / 1000 for time in final_store_time]
final_object_time[:] = [(time - final_object_time[0]) / 1000 for time in final_object_time]

final_store_dict = {"final store":final_store_recall,
                     "time recalled":final_store_time}

final_object_dict = {"final object":final_object_recall,
                     "time recalled":final_object_time}

final_store_df = pd.DataFrame.from_dict(final_store_dict, orient="index")
final_object_df = pd.DataFrame.from_dict(final_object_dict, orient="index")
print("Final store recall")
print(final_store_df)
print("\n\n")
print("Final free recall")
print(final_object_df)






