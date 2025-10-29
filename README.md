# 🧩 Customer Profile Dashboard (Apex + Visualforce)

A Salesforce **Visualforce dashboard** powered by an **Apex controller** that displays a 360° customer view — including Account, Primary Contact, Case History, and Active Subscriptions — in a single Lightning-styled interface.

---

## 🚀 Overview

**CustomerProfileDashboard_VF** is a demonstration project built to showcase Salesforce development expertise with **Apex**, **Visualforce**, and **SLDS (Salesforce Lightning Design System)**.

It combines **data retrieval**, **error handling**, and **Lightning UI styling** into one cohesive customer portal page.  
This dashboard allows Salesforce users to visualize key customer data such as open cases, closed cases, and active subscription contracts.

---

## ✨ Features

- **Unified Account View** – Shows Account, Contact, Case, and Subscription data together.  
- **Dynamic Contract Loading** – Filters and groups subscriptions by valid contract status.  
- **Lightning UI Styling** – Styled using SLDS for a modern Salesforce look.  
- **Error-Resilient Apex Logic** – Graceful exception handling via `ApexPages.addMessage`.  
- **Portfolio-Ready** – Clean documentation, clear structure, and professional presentation.

---

## 🧱 Technical Stack

| Layer | Technology | Purpose |
|-------|-------------|----------|
| Backend | **Apex (Salesforce)** | Business logic controller |
| Frontend | **Visualforce Page** | User interface & SLDS integration |
| Data | **Salesforce Objects** | Account, Case, Contract, SBQQ__Subscription__c |
| Styling | **SLDS (Salesforce Lightning Design System)** | Responsive layout and styling |

---

## 🧩 Architecture

This component retrieves and structures data in the following hierarchy:

