import csv
import json
import re

def clean_header(text):
    return text.strip() if text else ""

def clean_attribute(text):
    # Remove spaces: "Off The Ball" -> "OffTheBall"
    # Fix typos like "Natual Fitness" -> "NaturalFitness"
    text = text.strip().replace(" ", "")
    if text == "NatualFitness": return "NaturalFitness"
    return text

def generate_id(phase, category, name):
    # slugify: "InPossession", "Center Back", "Ball-Playing Center-Back"
    # -> "inpossession-center-back-ball-playing-center-back"
    cat_slug = category.lower().replace(" ", "-")
    name_slug = name.lower().replace(" ", "-")
    return f"{phase.lower()}-{cat_slug}-{name_slug}"

def parse_csv(filename, phase):
    roles = []
    
    with open(filename, 'r', encoding='utf-8-sig') as f:
        reader = csv.reader(f)
        rows = list(reader)
        
    # Row 0: Categories (e.g., "Striker", "Wing")
    # Row 1: Role Names (e.g., "Deep-Lying Forward")
    # Row 2+: Attributes
    
    if len(rows) < 3:
        print(f"Skipping {filename}: Not enough rows.")
        return []

    categories = rows[0]
    role_names = rows[1]
    
    # Initialize Role Objects
    # We skip column 0 (Attribute Name column)
    for col_idx in range(1, len(role_names)):
        role_name = clean_header(role_names[col_idx])
        category = clean_header(categories[col_idx])
        
        # Skip columns without a Role Name (empty spacers)
        if not role_name:
            continue
            
        role_id = generate_id(phase, category, role_name)
        
        roles.append({
            "col_idx": col_idx, # Temp index to map attributes later
            "data": {
                "Id": role_id,
                "Name": role_name,
                "Category": category,
                "Phase": phase,
                "Weights": {}
            }
        })

    # Iterate Attribute Rows
    for row_idx in range(2, len(rows)):
        row = rows[row_idx]
        if not row: continue
        
        raw_attr = row[0]
        if not raw_attr: continue
        
        attr_name = clean_attribute(raw_attr)
        
        # For each role, grab the value from its specific column
        for role in roles:
            col = role["col_idx"]
            if col < len(row):
                val = row[col].strip().lower()
                
                weight = 0
                if val == "primary":
                    weight = 3
                elif val == "secondary":
                    weight = 2
                
                if weight > 0:
                    role["data"]["Weights"][attr_name] = weight

    # Return just the data objects, stripping the helper 'col_idx'
    return [r["data"] for r in roles]

def main():
    all_roles = []
    
    # 1. Process In-Possession
    print("Processing In-Possession...")
    try:
        in_roles = parse_csv("in_possession.csv", "InPossession")
        all_roles.extend(in_roles)
        print(f" -> Found {len(in_roles)} roles.")
    except FileNotFoundError:
        print("Error: Could not find 'in_possession.csv'")

    # 2. Process Out-Of-Possession
    print("Processing Out-Of-Possession...")
    try:
        out_roles = parse_csv("out_possession.csv", "OutPossession")
        all_roles.extend(out_roles)
        print(f" -> Found {len(out_roles)} roles.")
    except FileNotFoundError:
        print("Error: Could not find 'out_possession.csv'")

    # 3. Write JSON
    output_file = "roles.json"
    with open(output_file, "w", encoding="utf-8") as f:
        json.dump(all_roles, f, indent=2)
    
    print(f"\nSuccess! Generated {len(all_roles)} roles in '{output_file}'.")

if __name__ == "__main__":
    main()