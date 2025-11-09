public without sharing class ms_customerProfileAPEX {

    // --- PROPERTIES ---
    private final Id accountId;
    public String lightningBaseUrl { get; private set; }
    public Contact primaryContact { get; private set; }
    public List<Case> openCases { get; private set; }
    public List<Case> closedCases { get; private set; }

    private final Set<String> VALID_STATUSES = new Set<String>{
        'Suspended', 'Pending Activation', 'Activated'
    };

    public Map<String, List<SBQQ__Subscription__c>> validContractsMap { get; private set; }
    private Map<Id, Contract> contractStore { get; set; }
    public Map<Id, Decimal> contractMRR { get; private set; } // Store total MRR per contract

    // --- CONSTRUCTOR ---
    public ms_customerProfileAPEX(ApexPages.StandardController stdController) {
        this.accountId = stdController.getId();

        if (System.Url.getOrgDomainUrl() != null) {
            lightningBaseUrl = System.Url.getOrgDomainUrl().toExternalForm();
        }

        validContractsMap = new Map<String, List<SBQQ__Subscription__c>>();
        contractStore = new Map<Id, Contract>();
        contractMRR = new Map<Id, Decimal>();
        openCases = new List<Case>();
        closedCases = new List<Case>();

        loadPrimaryContact();
        loadCases();
        loadSubscriptions();
    }

    // --- HELPER METHODS ---

    private void loadPrimaryContact() {
        try {
            List<Contract> contracts = [
                SELECT 
                    SBQQ__Quote__r.SBQQ__PrimaryContact__c, 
                    SBQQ__Quote__r.SBQQ__PrimaryContact__r.Name, 
                    SBQQ__Quote__r.SBQQ__PrimaryContact__r.Email, 
                    SBQQ__Quote__r.SBQQ__PrimaryContact__r.Phone, 
                    SBQQ__Quote__r.SBQQ__PrimaryContact__r.MobilePhone
                FROM Contract 
                WHERE AccountId = :this.accountId 
                  AND Status = 'Activated' 
                  AND SBQQ__Quote__r.SBQQ__PrimaryContact__c != NULL 
                ORDER BY StartDate DESC 
                LIMIT 1
            ];

            if (!contracts.isEmpty()) {
                this.primaryContact = contracts[0].SBQQ__Quote__r.SBQQ__PrimaryContact__r;
            }

        } catch (Exception e) {
            ApexPages.addMessage(new ApexPages.Message(
                ApexPages.Severity.WARNING, 
                'Could not load primary contact: ' + e.getMessage()
            ));
        }
    }

    private void loadCases() {
        try {
            this.openCases = [
                SELECT Id, CaseNumber, Subject, Status, CreatedDate 
                FROM Case 
                WHERE AccountId = :this.accountId AND IsClosed = false 
                ORDER BY CreatedDate DESC
                LIMIT 1000
            ];

            this.closedCases = [
                SELECT Id, CaseNumber, Subject, Status, ClosedDate 
                FROM Case 
                WHERE AccountId = :this.accountId AND IsClosed = true 
                ORDER BY ClosedDate DESC 
                LIMIT 20
            ];

        } catch (Exception e) {
            ApexPages.addMessage(new ApexPages.Message(
                ApexPages.Severity.ERROR, 
                'Error loading cases: ' + e.getMessage()
            ));
        }
    }

    private void loadSubscriptions() {
        try {
            List<SBQQ__Subscription__c> allSubscriptions = [ 
                SELECT 
                    Id, 
                    SBQQ__Contract__r.Id, 
                    SBQQ__Contract__r.ContractNumber,
                    SBQQ__Contract__r.Status,
                    SBQQ__Contract__r.ContractTerm,
                    SBQQ__Contract__r.EndDate, 
                    SBQQ__Contract__r.Kareo_ID__c, 
                    SBQQ__Contract__r.StartDate,
                    SBQQ__Contract__r.Pricing_Family__c,
                    SBQQ__Product__r.Name, 
                    SBQQ__Product__r.IsActive,
                    Practice_Name__c,
                    SBQQ__Product__r.Simplified_Product_Name__c,
                    Provider_License_Type__c,
                    SBQQ__Quantity__c,
                    SBQQ__RequiredById__c,
                    OSCPQ_Net_MRR__c
                FROM SBQQ__Subscription__c
                WHERE SBQQ__TerminatedDate__c = NULL
                  AND SBQQ__Account__c = :this.accountId 
               // ORDER BY Practice_Name__c DESC
            ];

            sortSubscriptions(allSubscriptions);

        } catch (Exception e) {
            ApexPages.addMessage(new ApexPages.Message(
                ApexPages.Severity.ERROR, 
                'Error loading subscriptions: ' + e.getMessage()
            ));
        }
    }

    /**
     * @description Private helper method to sort subscriptions and calculate MRR rollup.
     */
    private void sortSubscriptions(List<SBQQ__Subscription__c> allSubscriptions) {

        // --- Group by contract and accumulate MRR ---
        for (SBQQ__Subscription__c sub : allSubscriptions) {
            if (sub.SBQQ__Contract__r == null || sub.SBQQ__Contract__r.Status == null) continue;
            if (!VALID_STATUSES.contains(sub.SBQQ__Contract__r.Status)) continue;

            Id contractId = sub.SBQQ__Contract__r.Id;
            String contractIdString = String.valueOf(contractId);

            if (!validContractsMap.containsKey(contractIdString)) {
                validContractsMap.put(contractIdString, new List<SBQQ__Subscription__c>());
            }
            validContractsMap.get(contractIdString).add(sub);

            if (!contractStore.containsKey(contractId)) {
                contractStore.put(contractId, sub.SBQQ__Contract__r);
            }

            Decimal thisMRR = (sub.OSCPQ_Net_MRR__c == null) ? 0 : sub.OSCPQ_Net_MRR__c;
            if (!contractMRR.containsKey(contractId)) {
                contractMRR.put(contractId, thisMRR);
            } else {
                contractMRR.put(contractId, contractMRR.get(contractId) + thisMRR);
            }
        }

        // --- Reorganize and sort subscriptions per contract ---
        for (String cId : validContractsMap.keySet()) {
            List<SBQQ__Subscription__c> subs = validContractsMap.get(cId);

            Map<Id, List<SBQQ__Subscription__c>> childrenMap = new Map<Id, List<SBQQ__Subscription__c>>();
            List<SBQQ__Subscription__c> parents = new List<SBQQ__Subscription__c>();

            // Separate parents vs children
            for (SBQQ__Subscription__c s : subs) {
                if (s.SBQQ__RequiredById__c == null) {
                    parents.add(s);
                } else {
                    if (!childrenMap.containsKey(s.SBQQ__RequiredById__c)) {
                        childrenMap.put(s.SBQQ__RequiredById__c, new List<SBQQ__Subscription__c>());
                    }
                    childrenMap.get(s.SBQQ__RequiredById__c).add(s);
                }
            }

            // --- Sort parents by Product Name ---
            Map<String, List<SBQQ__Subscription__c>> parentsByName = new Map<String, List<SBQQ__Subscription__c>>();
            for (SBQQ__Subscription__c p : parents) {
                String pname = (p.SBQQ__Product__r != null && p.SBQQ__Product__r.Name != null) ? p.SBQQ__Product__r.Name : '';
                if (!parentsByName.containsKey(pname)) parentsByName.put(pname, new List<SBQQ__Subscription__c>());
                parentsByName.get(pname).add(p);
            }

            List<String> parentNames = new List<String>(parentsByName.keySet());
            parentNames.sort();

            List<SBQQ__Subscription__c> orderedParents = new List<SBQQ__Subscription__c>();
            for (String pn : parentNames) {
                orderedParents.addAll(parentsByName.get(pn));
            }

            // --- Combine parents and sorted children ---
            List<SBQQ__Subscription__c> ordered = new List<SBQQ__Subscription__c>();
            for (SBQQ__Subscription__c p : orderedParents) {
                ordered.add(p);

                if (childrenMap.containsKey(p.Id)) {
                    List<SBQQ__Subscription__c> childList = childrenMap.get(p.Id);

                    // Sort children alphabetically by Product Name
                    Map<String, List<SBQQ__Subscription__c>> childByName = new Map<String, List<SBQQ__Subscription__c>>();
                    for (SBQQ__Subscription__c cs : childList) {
                        String prodName = (cs.SBQQ__Product__r != null && cs.SBQQ__Product__r.Name != null) ? cs.SBQQ__Product__r.Name : '';
                        if (!childByName.containsKey(prodName)) childByName.put(prodName, new List<SBQQ__Subscription__c>());
                        childByName.get(prodName).add(cs);
                    }

                    List<String> childNames = new List<String>(childByName.keySet());
                    childNames.sort();

                    for (String nm : childNames) {
                        ordered.addAll(childByName.get(nm));
                    }
                }
            }

            // Ensure no subs are lost
            for (SBQQ__Subscription__c s : subs) {
                if (!ordered.contains(s)) ordered.add(s);
            }

            validContractsMap.put(cId, ordered);
        }
    }

    /**
     * @description Return all valid Contracts sorted by default ordering.
     */
    public List<Contract> getValidContracts() {
        if (contractStore == null) {
            return new List<Contract>();
        }
        List<Contract> contracts = new List<Contract>(contractStore.values());
        contracts.sort();
        return contracts;
    }
}